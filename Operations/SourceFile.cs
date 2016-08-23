using System;
using System.IO;
using System.Xml;
using System.Linq;
using Inedo.IO;
using Inedo.Agents;
using System.Threading.Tasks;
using System.Threading;

namespace Inedo.Extensions.WiX.Operations
{
    /// <summary>
    /// Represents a file to be included in a WiX package.
    /// </summary>
    internal sealed class SourceFile
    {
        private SlimFileInfo mappedFile;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceFile"/> class.
        /// </summary>
        private SourceFile()
        {
        }

        public Guid Id { get; private set; }
        public SlimFileInfo Source { get; private set; }
        public string FileName
        {
            get { return this.Source.Name; }
        }
        public SlimFileInfo PhysicalFile
        {
            get { return this.mappedFile ?? this.Source; }
        }

        public async Task<bool> IsIdenticalAsync(IFileOperationsExecuter fileOps, SlimFileInfo filePath, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!string.Equals(this.FileName, filePath.Name, StringComparison.InvariantCultureIgnoreCase))
                return false;

            using (var file1 = await fileOps.OpenFileAsync(this.PhysicalFile.FullName, FileMode.Open, FileAccess.Read).ConfigureAwait(false))
            using (var file2 = await fileOps.OpenFileAsync(filePath.FullName, FileMode.Open, FileAccess.Read).ConfigureAwait(false))
            {
                try
                {
                    if (file1.Length != file2.Length)
                        return false;
                }
                catch (NotSupportedException)
                {
                    // can't get the length, assume they might be the same
                }

                byte[] buffer1 = new byte[8192];
                byte[] buffer2 = new byte[8192];

                while (true)
                {
                    int len = await file1.ReadAsync(buffer1, 0, buffer1.Length, cancellationToken).ConfigureAwait(false);
                    if (len == 0)
                    {
                        // make sure both files are at EOF
                        len = await file2.ReadAsync(buffer2, 0, buffer2.Length, cancellationToken).ConfigureAwait(false);
                        return len == 0;
                    }

                    if (!await ReadFullAsync(file2, buffer2, len, cancellationToken).ConfigureAwait(false))
                        return false;

                    if (!buffer1.Take(len).SequenceEqual(buffer2.Take(len)))
                        return false;
                }
            }
        }

        private static async Task<bool> ReadFullAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken = default(CancellationToken))
        {
            int offset = 0;
            while (count > 0)
            {
                int n = await stream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                if (n == 0)
                {
                    return false;
                }
                offset += n;
                count -= n;
            }
            return true;
        }

        public SourceFile CreateReference(SlimFileInfo sourceFile)
        {
            return new SourceFile()
            {
                Id = Guid.NewGuid(),
                Source = sourceFile,
                mappedFile = this.mappedFile ?? this.Source
            };
        }
        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("Component");
            writer.WriteAttributeString("Id", "cmp" + this.Id.ToString("N").ToUpper());
            writer.WriteAttributeString("Guid", this.Id.ToString("B"));
            writer.WriteStartElement("File");
            writer.WriteAttributeString("Id", "fil" + this.Id.ToString("N").ToUpper());
            writer.WriteAttributeString("KeyPath", "yes");
            writer.WriteAttributeString("Source", this.PhysicalFile.FullName);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        public void WriteReferenceXml(XmlWriter writer)
        {
            writer.WriteStartElement("ComponentRef");
            writer.WriteAttributeString("Id", "cmp" + this.Id.ToString("N").ToUpper());
            writer.WriteEndElement();
        }
        public static SourceFile Create(SlimFileInfo sourceFile)
        {
            return new SourceFile()
            {
                Id = Guid.NewGuid(),
                Source = sourceFile
            };
        }

        public override string ToString()
        {
            return this.Source.Name;
        }
    }
}
