using System.ComponentModel.DataAnnotations;
using IdentityServerTemplate.Pages.Admin.Federation.Extensions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IdentityServerTemplate.Pages.Admin.Federation;

public class ProviderConfigurationModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType = context.Metadata.UnderlyingOrModelType;
        if (modelType == typeof(IProviderConfigurationModel))
        {
            var providerConfigurationModelFactories = context.Services.GetRequiredService<IEnumerable<IProviderConfigurationModelFactory>>();
            var binderFactory = (ModelMetadata metadata) => context.CreateBinder(metadata);
            return new ProviderConfigurationModelBinder(providerConfigurationModelFactories, binderFactory);
        }

        return null;
    }
}

public class ProviderConfigurationModelBinder(
    IEnumerable<IProviderConfigurationModelFactory> providerConfigurationModelFactories,
    Func<ModelMetadata, IModelBinder> binderFactory) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        // Only try binding when the object is not top level
        if (bindingContext.IsTopLevelObject)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        // Find Type property on parent object
        var typePropertyName = bindingContext.ModelName.Replace(bindingContext.FieldName, "") + "Type";
        var typePropertyValueProviderResult = bindingContext.ValueProvider.GetValue(typePropertyName);
        if (typePropertyValueProviderResult == ValueProviderResult.None)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        // Find the provider configuration model factory for the given provider type
        var providerConfigurationModelFactory = providerConfigurationModelFactories.FirstOrDefault(x => x.SupportsType(typePropertyValueProviderResult.FirstValue ?? ""));
        if (providerConfigurationModelFactory == null)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        // Create model and try binding to it
        var model = providerConfigurationModelFactory.Create();

        var metadata = bindingContext.ModelMetadata.GetMetadataForType(model.GetType());
        var concreteBinder = binderFactory(metadata);
        var concreteBindingContext = DefaultModelBindingContext.CreateBindingContext(
            actionContext: bindingContext.ActionContext,
            valueProvider: bindingContext.ValueProvider,
            metadata: metadata,
            bindingInfo: null,
            modelName: bindingContext.ModelName);

        await concreteBinder.BindModelAsync(concreteBindingContext);
        bindingContext.Result = concreteBindingContext.Result;

        // Perform model validation
        if (bindingContext.Result.IsModelSet)
        {
            // Mark as validated
            foreach (var keyValuePair in bindingContext.ModelState.FindKeysWithPrefix(bindingContext.ModelName))
            {
                keyValuePair.Value.ValidationState = ModelValidationState.Valid;
            }

            // Run new validation
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(bindingContext.Result.Model!);
            if (!Validator.TryValidateObject(bindingContext.Result.Model!, validationContext, validationResults, true))
            {
                foreach (var result in validationResults)
                {
                    if (!result.MemberNames.Any())
                    {
                        bindingContext.ModelState.AddModelError(string.Empty, result.ErrorMessage!);
                    }
                    else
                    {
                        foreach (var name in result.MemberNames)
                        {
                            bindingContext.ModelState.AddModelError(ModelNames.CreatePropertyModelName(bindingContext.ModelName, name), result.ErrorMessage!);
                        }
                    }
                }
            }

            bindingContext.ValidationState[bindingContext.Result.Model!] = new ValidationStateEntry
            {
                Metadata = bindingContext.ModelMetadata
            };
        }
    }
}
