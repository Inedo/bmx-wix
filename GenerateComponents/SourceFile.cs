using System;
using System.IO;
using System.Xml;
using System.Linq;

namespace Inedo.BuildMasterExtensions.WiX.GenerateComponents
{
    /// <summary>
    /// Represents a file to be included in a WiX package.
    /// </summary>
    internal sealed class SourceFile
    {
        private FileInfo mappedFile;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceFile"/> class.
        /// </summary>
        private SourceFile()
        {
        }

        public Guid Id { get; private set; }
        public FileInfo Source { get; private set; }
        public string FileName
        {
            get { return this.Source.Name; }
        }
        public FileInfo PhysicalFile
        {
            get { return this.mappedFile ?? this.Source; }
        }

        public bool IsIdentical(FileInfo filePath)
        {
            if (!string.Equals(this.FileName, filePath.Name, StringComparison.InvariantCultureIgnoreCase))
                return false;

            using (var file1 = this.PhysicalFile.OpenRead())
            using (var file2 = filePath.OpenRead())
            {
                if (file1.Length != file2.Length)
                    return false;

                byte[] buffer1 = new byte[8192];
                byte[] buffer2 = new byte[8192];

                while (true)
                {
                    int len = file1.Read(buffer1, 0, buffer1.Length);
                    if (len == 0)
                        return true;

                    file2.Read(buffer2, 0, len);
                    if (!buffer1.Take(len).SequenceEqual(buffer2.Take(len)))
                        return false;
                }
            }
        }
        public SourceFile CreateReference(FileInfo sourceFile)
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
        public static SourceFile Create(FileInfo sourceFile)
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
