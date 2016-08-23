using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Inedo.Agents;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.IO;
using Inedo.Serialization;

namespace Inedo.Extensions.WiX.Operations
{
    /// <summary>
    /// Generates a WiX fragment based on the files in a directory.
    /// </summary>
    [DisplayName("Generate Components")]
    [Description("Generates a WiX fragment based on the files in a directory.")]
    [Tag("WiX")]
    public sealed class GenerateComponentsOperation : ExecuteOperation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateComponentsOperation"/> class.
        /// </summary>
        public GenerateComponentsOperation()
        {
        }

        /// <summary>
        /// Gets or sets the name of the WiX fragment file to generate.
        /// </summary>
        [Persistent]
        [Required]
        [DisplayName("Fragment File")]
        [Description("The name of the WiX fragment file (.wxs) to generate.")]
        public string FragmentFileName { get; set; }

        [Persistent]
        [Required]
        [DisplayName("Source Directory")]
        public string SourceDirectory { get; set; }

        [Persistent]
        [Required]
        [DisplayName("Target Directory")]
        public string TargetDirectory { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (string.IsNullOrEmpty(this.FragmentFileName))
            {
                this.LogWarning("Fragment file name not specified; cannot generate components.");
                return;
            }

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            var rootDir = await fileOps.GetDirectoryInfoAsync(this.SourceDirectory).ConfigureAwait(false);
            var outFile = Path.Combine(this.TargetDirectory, this.FragmentFileName);

            this.LogInformation(string.Format("Generating {0} from {1}", outFile, rootDir.FullName));

            var files = await GetSourceFilesAsync(fileOps, rootDir.FullName, context.CancellationToken).ConfigureAwait(false);
            var groups = (await fileOps.GetFileSystemInfosAsync(rootDir.FullName, MaskingContext.Default).ConfigureAwait(false))
                .Where(s => s.Attributes.HasFlag(FileAttributes.Directory) && s.Name != "." && s.Name != "..").Select(s => s.FullName);

            await fileOps.DeleteFileAsync(outFile);

            using (var xmlWriter = XmlWriter.Create(outFile, new XmlWriterSettings() { Indent = true }))
            {
                xmlWriter.WriteStartElement("Wix", "http://schemas.microsoft.com/wix/2006/wi");
                xmlWriter.WriteStartElement("Fragment");

                foreach (var group in groups)
                {
                    var filesInGroup = files.Where(f => f.Source.DirectoryName.StartsWith(group));

                    xmlWriter.WriteStartElement("DirectoryRef");
                    xmlWriter.WriteAttributeString("Id", "TARGETDIR");

                    var currentDir = rootDir;
                    var dirs = GetChildDirectories(currentDir, filesInGroup)
                        .Select(d => d.FullName)
                        .Distinct();

                    foreach (var dir in dirs)
                        WriteXml(xmlWriter, dir, files, true);

                    xmlWriter.WriteEndElement();
                }

                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Fragment");

                foreach (var group in groups)
                {
                    var groupName = Path.GetFileName(group.Substring(rootDir.FullName.Length + 1));

                    this.LogInformation("Generating component: " + groupName);

                    xmlWriter.WriteStartElement("ComponentGroup");
                    xmlWriter.WriteAttributeString("Id", groupName);

                    var filesInGroup = files.Where(f => f.Source.DirectoryName.StartsWith(group));
                    foreach (var component in filesInGroup)
                        component.WriteReferenceXml(xmlWriter);

                    xmlWriter.WriteEndElement();
                }

                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndElement();
            }

            this.LogInformation("Finished generating components.");
        }

        private static void WriteXml(XmlWriter xmlWriter, string dir, IEnumerable<SourceFile> files, bool useReadableDirId)
        {
            xmlWriter.WriteStartElement("Directory");
            if (!useReadableDirId)
                xmlWriter.WriteAttributeString("Id", "dir" + Guid.NewGuid().ToString("N").ToUpper());
            else
                xmlWriter.WriteAttributeString("Id", "dir" + Path.GetFileName(dir));

            xmlWriter.WriteAttributeString("Name", Path.GetFileName(dir));

            foreach (var file in files.Where(f => f.Source.DirectoryName == dir))
                file.WriteXml(xmlWriter);

            var dirs = GetChildDirectories(ToDirectory(dir), files)
                .Select(d => d.FullName).Distinct();

            foreach (var childDir in dirs)
                WriteXml(xmlWriter, childDir, files, false);

            xmlWriter.WriteEndElement();
        }

        private static SlimDirectoryInfo ToDirectory(string path)
        {
            return new SlimDirectoryInfo(path, default(DateTime), FileAttributes.Directory);
        }

        private static IEnumerable<SlimDirectoryInfo> GetChildDirectories(SlimDirectoryInfo parent, IEnumerable<SourceFile> files)
        {
            return files
                .Where(f => f.Source.DirectoryName != parent.FullName && f.Source.DirectoryName.StartsWith(parent.FullName))
                .Select(f => GetTopMostChild(parent, ToDirectory(f.Source.DirectoryName)));
        }

        private static SlimDirectoryInfo GetTopMostChild(SlimDirectoryInfo parent, SlimDirectoryInfo dir)
        {
            var parentString = parent.FullName;

            while (dir.DirectoryName != parentString)
            {
                dir = ToDirectory(dir.DirectoryName);
            }

            return dir;
        }

        private static async Task<List<SourceFile>> GetSourceFilesAsync(IFileOperationsExecuter fileOps, string rootPath, CancellationToken cancellationToken = default(CancellationToken))
        {
            var files = await fileOps.GetFileSystemInfosAsync(rootPath, MaskingContext.IncludeAll).ConfigureAwait(false);
            var components = new List<SourceFile>();

            foreach (var fileInfo in files)
            {
                var file = fileInfo as SlimFileInfo;
                if (file == null)
                    continue;

                var found = false;
                foreach (var c in components)
                {
                    if (await c.IsIdenticalAsync(fileOps, file, cancellationToken).ConfigureAwait(false))
                    {
                        components.Add(c.CreateReference(file));
                        found = true;
                        break;
                    }
                }

                if (!found)
                    components.Add(SourceFile.Create(file));
            }

            return components;
        }

        private static string StripRoot(string rootPath, string path)
        {
            return path.Substring(rootPath.Length).TrimStart(Path.DirectorySeparatorChar);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Generate ", new Hilite(config[nameof(this.FragmentFileName)])),
                new RichDescription(" based on the contents of ", new DirectoryHilite(config[nameof(this.SourceDirectory)]))
            );
        }
    }
}
