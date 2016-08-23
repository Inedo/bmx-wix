using System;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.Extensions.WiX.Operations;

namespace Inedo.BuildMasterExtensions.WiX.GenerateComponents
{
    public sealed class GenerateComponentsActionConverter : IActionOperationConverter<GenerateComponentsAction, GenerateComponentsOperation>
    {
        public ConvertedOperation<GenerateComponentsOperation> ConvertActionToOperation(GenerateComponentsAction action, IActionConverterContext context)
        {
            return new GenerateComponentsOperation()
            {
                FragmentFileName = action.FragmentFileName,
                SourceDirectory = action.OverriddenSourceDirectory,
                TargetDirectory = action.OverriddenTargetDirectory
            };
        }
    }
}
