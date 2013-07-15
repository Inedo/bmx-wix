using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.WiX.UpdateProduct
{
    /// <summary>
    /// Custom editor for the Update Product action.
    /// </summary>
    internal sealed class UpdateProductActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtFileName;
        private ValidatingTextBox txtProductId;
        private ValidatingTextBox txtVersion;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateProductActionEditor"/> class.
        /// </summary>
        public UpdateProductActionEditor()
        {
        }

        public override bool DisplaySourceDirectory
        {
            get { return true; }
        }
        public override void BindToForm(ActionBase action)
        {
            EnsureChildControls();

            var updateAction = (UpdateProductAction)action;
            this.txtFileName.Text = updateAction.SourceFile;
            this.txtProductId.Text = updateAction.ProductId ?? string.Empty;
            this.txtVersion.Text = updateAction.ProductVersion ?? string.Empty;
        }
        public override ActionBase CreateFromForm()
        {
            EnsureChildControls();

            return new UpdateProductAction()
            {
                SourceFile = this.txtFileName.Text,
                ProductId = this.txtProductId.Text,
                ProductVersion = this.txtVersion.Text
            };
        }

        protected override void CreateChildControls()
        {
            this.txtFileName = new ValidatingTextBox()
            {
                Width = 300,
                Required = true
            };

            this.txtProductId = new ValidatingTextBox()
            {
                Width = 300,
                Required = false
            };

            this.txtVersion = new ValidatingTextBox()
            {
                Width = 300,
                Required = true,
                Text = "%RELNO%.%BLDNO%"
            };

            CUtil.Add(this,
                new FormFieldGroup(
                    "Source File",
                    "Specify the name of the WiX source file containing a Product element.",
                    false,
                    new StandardFormField(
                        string.Empty,
                        this.txtFileName
                    )
                ),
                new FormFieldGroup(
                    "Product Id",
                    "Provide the Product Id Guid if desired. To autogenerate a Product Id, leave this field blank.",
                    false,
                    new StandardFormField(
                        string.Empty,
                        this.txtProductId
                    )
                ),
                new FormFieldGroup(
                    "Product Version",
                    "Specify the Product Version number.",
                    false,
                    new StandardFormField(
                        string.Empty,
                        this.txtVersion
                    )
                )
            );
        }
    }
}
