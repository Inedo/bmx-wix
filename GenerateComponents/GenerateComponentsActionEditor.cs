using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.WiX.GenerateComponents
{
    /// <summary>
    /// Custom editor for the Generate Components action.
    /// </summary>
    internal sealed class GenerateComponentsActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtFileName;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateComponentsActionEditor"/> class.
        /// </summary>
        public GenerateComponentsActionEditor()
        {
        }

        public override bool DisplaySourceDirectory
        {
            get { return true; }
        }
        public override bool DisplayTargetDirectory
        {
            get { return true; }
        }

        protected override void CreateChildControls()
        {
            this.txtFileName = new ValidatingTextBox()
            {
                Width = 300,
                Required = true
            };

            CUtil.Add(this,
                new FormFieldGroup(
                    "Fragment File",
                    "The name of the WiX fragment file (.wxs) to generate.",
                    false,
                    new StandardFormField(
                        string.Empty,
                        this.txtFileName
                    )
                )
            );
        }

        public override void BindToForm(ActionBase action)
        {
            EnsureChildControls();

            var gca = (GenerateComponentsAction)action;
            this.txtFileName.Text = gca.FragmentFileName;
        }

        public override ActionBase CreateFromForm()
        {
            EnsureChildControls();

            return new GenerateComponentsAction()
            {
                FragmentFileName = this.txtFileName.Text
            };
        }
    }
}
