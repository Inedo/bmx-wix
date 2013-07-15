using System;
using System.IO;
using System.Xml;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web;

namespace Inedo.BuildMasterExtensions.WiX.UpdateProduct
{
    /// <summary>
    /// Updates attributes on a WiX Product element.
    /// </summary>
    [ActionProperties("Update Product", "Updates attributes on a WiX Product element.", "WiX")]
    [CustomEditor(typeof(UpdateProductActionEditor))]
    public sealed class UpdateProductAction : RemoteActionBase
    {
        /// <summary>
        /// Namespace URI for WiX source files.
        /// </summary>
        private const string NamespaceUri = "http://schemas.microsoft.com/wix/2006/wi";

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateProductAction"/> class.
        /// </summary>
        public UpdateProductAction()
        {
        }

        /// <summary>
        /// Gets or sets the WiX Product Id to write. This may either be a Guid or a null/empty string.
        /// </summary>
        /// <remarks>
        /// A null or empty string indicates that a new Product Id should be generated.
        /// </remarks>
        [Persistent]
        public string ProductId { get; set; }
        /// <summary>
        /// Gets or sets the WiX Product Version to write.
        /// </summary>
        [Persistent]
        public string ProductVersion { get; set; }
        /// <summary>
        /// Gets or sets the name of the WiX source file to update.
        /// </summary>
        [Persistent]
        public string SourceFile { get; set; }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var path = Util.CoalesceStr(this.OverriddenSourceDirectory, "(default directory)");

            if (!string.IsNullOrEmpty(this.ProductId))
                return string.Format("Write Product Id '{0}' and Product Version '{1}' to {2} in {3}.", this.ProductId, this.ProductVersion, this.SourceFile, path);
            else
                return string.Format("Write new Product Id and Product Version '{0}' to {1} in {2}.", this.ProductVersion, this.SourceFile, path);
        }

        protected override void Execute()
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

            ExecuteRemoteCommand("update", productId);
        }
        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            var productId = args[0];

            var fileName = Path.Combine(this.RemoteConfiguration.SourceDirectory, this.SourceFile);
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(fileName);

            var nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
            nsManager.AddNamespace("d", NamespaceUri);

            var productNode = (XmlElement)xmlDoc.SelectSingleNode("/d:Wix/d:Product", nsManager);
            if (productNode == null)
            {
                LogWarning("WiX source file does not contain a Product element.");
                return string.Empty;
            }

            LogInformation(string.Format("Updating Product Id to {0} and Version to {1} in {2}...", productId, this.ProductVersion, fileName));

            productNode.SetAttribute("Id", productId);
            productNode.SetAttribute("Version", this.ProductVersion);

            var fileAttr = File.GetAttributes(fileName);
            if ((fileAttr & FileAttributes.ReadOnly) != 0)
            {
                fileAttr &= ~FileAttributes.ReadOnly;
                File.SetAttributes(fileName, fileAttr);
            }

            xmlDoc.Save(fileName);
            LogInformation(string.Format("{0} updated.", fileName));

            return string.Empty;
        }
    }
}
