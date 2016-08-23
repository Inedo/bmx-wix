using System;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.Extensions.WiX.Operations;

namespace Inedo.BuildMasterExtensions.WiX.UpdateProduct
{
    public sealed class UpdateProductActionConverter : IActionOperationConverter<UpdateProductAction, UpdateProductOperation>
    {
        public ConvertedOperation<UpdateProductOperation> ConvertActionToOperation(UpdateProductAction action, IActionConverterContext context)
        {
            return new UpdateProductOperation()
            {
                ProductId = action.ProductId,
                ProductVersion = context.ConvertLegacyExpression(action.ProductVersion),
                SourceFile = action.SourceFile,
                SourceDirectory = action.OverriddenSourceDirectory,
                TargetDirectory = action.OverriddenTargetDirectory
            };
        }
    }
}
