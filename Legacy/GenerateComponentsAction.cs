using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml;
using Inedo.Agents;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.Documentation;
using Inedo.Serialization;

namespace Inedo.BuildMasterExtensions.WiX.GenerateComponents
{
    /// <summary>
    /// Generates a WiX fragment based on the files in a directory.
    /// </summary>
    [DisplayName("Generate Components")]
    [Description("Generates a WiX fragment based on the files in a directory.")]
    [Tag("WiX")]
    [ConvertibleToOperation(typeof(GenerateComponentsActionConverter))]
    public sealed class GenerateComponentsAction : RemoteActionBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateComponentsAction"/> class.
        /// </summary>
        public GenerateComponentsAction()
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

        protected override void Execute()
        {
            if (string.IsNullOrEmpty(this.FragmentFileName))
            {
                LogWarning("Fragment file name not specified; cannot generate components.");
                return;
            }

            ExecuteRemoteCommand("generate");
        }

        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            var rootDir = new DirectoryInfo(this.OverriddenSourceDirectory);
            var outFile = Path.Combine(this.OverriddenTargetDirectory, this.FragmentFileName);

            LogInformation(string.Format("Generating {0} from {1}", outFile, rootDir.FullName));

            var files = GetSourceFiles(rootDir.FullName);
            var groups = Directory.GetDirectories(rootDir.FullName, "*", SearchOption.TopDirectoryOnly)
                .Where(s => s != "." && s != "..");

            var fileOps = this.Context.Agent.GetService<IFileOperationsExecuter>();
            fileOps.DeleteFile(outFile);

            using (var xmlWriter = XmlTextWriter.Create(outFile, new XmlWriterSettings() { Indent = true }))
            {
                xmlWriter.WriteStartElement("Wix", "http://schemas.microsoft.com/wix/2006/wi");
                xmlWriter.WriteStartElement("Fragment");

                foreach (var group in groups)
                {
                    var filesInGroup = files.Where(f => f.Source.Directory.FullName.StartsWith(group));

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

                    LogInformation("Generating component: " + groupName);

                    xmlWriter.WriteStartElement("ComponentGroup");
                    xmlWriter.WriteAttributeString("Id", groupName);

                    var filesInGroup = files.Where(f => f.Source.Directory.FullName.StartsWith(group));
                    foreach (var component in filesInGroup)
                        component.WriteReferenceXml(xmlWriter);

                    xmlWriter.WriteEndElement();
                }

                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndElement();
            }

            LogInformation("Finished generating components.");

            return string.Empty;
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("Generate {0} file based on the contents of {1}", this.FragmentFileName, Util.CoalesceStr(this.OverriddenSourceDirectory, "(default)"));
        }

        private static void WriteXml(XmlWriter xmlWriter, string dir, IEnumerable<SourceFile> files, bool useReadableDirId)
        {
            xmlWriter.WriteStartElement("Directory");
            if (!useReadableDirId)
                xmlWriter.WriteAttributeString("Id", "dir" + Guid.NewGuid().ToString("N").ToUpper());
            else
                xmlWriter.WriteAttributeString("Id", "dir" + Path.GetFileName(dir));

            xmlWriter.WriteAttributeString("Name", Path.GetFileName(dir));

            foreach (var file in files.Where(f => f.Source.Directory.FullName == dir))
                file.WriteXml(xmlWriter);

            var dirs = GetChildDirectories(new DirectoryInfo(dir), files)
                .Select(d => d.FullName).Distinct();

            foreach (var childDir in dirs)
                WriteXml(xmlWriter, childDir, files, false);

            xmlWriter.WriteEndElement();
        }

        private static IEnumerable<DirectoryInfo> GetChildDirectories(DirectoryInfo parent, IEnumerable<SourceFile> files)
        {
            return files
                .Where(f => f.Source.Directory.FullName != parent.FullName && f.Source.Directory.FullName.StartsWith(parent.FullName))
                .Select(f => GetTopMostChild(parent, f.Source.Directory));
        }

        private static DirectoryInfo GetTopMostChild(DirectoryInfo parent, DirectoryInfo dir)
        {
            var parentString = parent.FullName;

            while (dir.Parent.FullName != parentString)
            {
                dir = dir.Parent;
            }

            return dir;
        }

        private static List<SourceFile> GetSourceFiles(string rootPath)
        {
            var files = new DirectoryInfo(rootPath).GetFiles("*", SearchOption.AllDirectories);
            var components = new List<SourceFile>();

            foreach (var file in files)
            {
                var match = components
                    .Where(c => c.IsIdentical(file))
                    .FirstOrDefault();

                if (match != null)
                    components.Add(match.CreateReference(file));
                else
                    components.Add(SourceFile.Create(file));
            }

            return components;
        }

        private static string StripRoot(string rootPath, string path)
        {
            return path.Substring(rootPath.Length).TrimStart(Path.DirectorySeparatorChar);
        }
    }
}
