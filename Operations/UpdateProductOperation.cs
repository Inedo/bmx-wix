using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.Documentation;
using Inedo.Serialization;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.IO;

namespace Inedo.Extensions.WiX.Operations
{
    /// <summary>
    /// Updates attributes on a WiX Product element.
    /// </summary>
    [DisplayName("Update Product")]
    [Description("Updates attributes on a WiX Product element.")]
    [Tag("WiX")]
    public sealed class UpdateProductOperation : ExecuteOperation
    {
        /// <summary>
        /// Namespace URI for WiX source files.
        /// </summary>
        private const string NamespaceUri = "http://schemas.microsoft.com/wix/2006/wi";

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateProductOperation"/> class.
        /// </summary>
        public UpdateProductOperation()
        {
        }

        /// <summary>
        /// Gets or sets the WiX Product Id to write. This may either be a Guid or a null/empty string.
        /// </summary>
        /// <remarks>
        /// A null or empty string indicates that a new Product Id should be generated.
        /// </remarks>
        [Persistent]
        [DisplayName("Product Id")]
        [Description("Provide the Product Id Guid if desired. To autogenerate a Product Id, leave this field blank.")]
        public string ProductId { get; set; }
        /// <summary>
        /// Gets or sets the WiX Product Version to write.
        /// </summary>
        [Persistent]
        [Required]
        [DisplayName("Product Version")]
        [Description("Specify the Product Version number.")]
        [DefaultValue("$RELNO.$BLDNO")]
        public string ProductVersion { get; set; } = "$RELNO.$BLDNO";
        /// <summary>
        /// Gets or sets the name of the WiX source file to update.
        /// </summary>
        [Persistent]
        [Required]
        [DisplayName("Source File")]
        [Description("Specify the name of the WiX source file containing a Product element.")]
        public string SourceFile { get; set; }

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
            var productId = Util.CoalesceStr(this.ProductId, Guid.NewGuid().ToString("D"));
            try
            {
                new Guid(productId);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Product Id is not a valid Guid.", ex);
            }

            try
            {
                new Version(this.ProductVersion);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Product Version is not a valid Major.Minor.Revision.Build version number.", ex);
            }

            if (string.IsNullOrEmpty(this.SourceFile))
                throw new InvalidOperationException("Source file is not specifed.");

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            var fileName = Path.Combine(this.SourceDirectory, this.SourceFile);
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(await fileOps.ReadAllTextAsync(fileName).ConfigureAwait(false));

            var nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
            nsManager.AddNamespace("d", NamespaceUri);

            var productNode = (XmlElement)xmlDoc.SelectSingleNode("/d:Wix/d:Product", nsManager);
            if (productNode == null)
            {
                this.LogWarning("WiX source file does not contain a Product element.");
                return;
            }

            this.LogInformation("Updating Product Id to {0} and Version to {1} in {2}...", productId, this.ProductVersion, fileName);

            productNode.SetAttribute("Id", productId);
            productNode.SetAttribute("Version", this.ProductVersion);

            var fileAttr = await fileOps.GetAttributesAsync(fileName).ConfigureAwait(false);
            if ((fileAttr & FileAttributes.ReadOnly) != 0)
            {
                fileAttr &= ~FileAttributes.ReadOnly;
                await fileOps.SetAttributesAsync(fileName, fileAttr).ConfigureAwait(false);
            }

            var save = new SlimMemoryStream();
            xmlDoc.Save(save);
            await fileOps.WriteFileBytesAsync(fileName, save.ToArray()).ConfigureAwait(false);
            this.LogInformation("{0} updated.", fileName);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                string.IsNullOrEmpty(config[nameof(this.ProductId)]) ?
                    new RichDescription("Write Product Id ", new Hilite(config[nameof(this.ProductId)]), " and Product Version ", new Hilite(config[nameof(this.ProductVersion)])) :
                    new RichDescription("Write new Product Id and Product Version ", new Hilite(config[nameof(this.ProductVersion)])),
                new RichDescription("to ", new Hilite(config[nameof(this.SourceFile)]), " in ", new DirectoryHilite(config[nameof(this.SourceDirectory)]))
            );
        }
    }
}
