using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace StudioCRM.Api.Swagger;

// JSON keeps enums as numbers (except types with their own string converter); this only documents the mapping.
public class EnumDescriptionSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Type) ?? context.Type;
        if (!type.IsEnum) return;
        var values = Enum.GetValues(type).Cast<object>()
            .Select(v => (Number: Convert.ToInt64(v), Name: Enum.GetName(type, v)!)).ToList();
        var converter = type.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType;
        var asString = converter is not null && (converter == typeof(JsonStringEnumConverter) ||
            converter.IsGenericType && converter.GetGenericTypeDefinition() == typeof(JsonStringEnumConverter<>));
        if (asString)
        {
            // e.g. PackageBillingMode has its own string converter; document it as text.
            schema.Type = "string";
            schema.Format = null;
            schema.Enum = values.Select(v => (IOpenApiAny)new OpenApiString(v.Name)).ToList();
            schema.Description = "Serializowane jako tekst: " + string.Join(", ", values.Select(v => v.Name));
            return;
        }
        schema.Description = string.Join(", ", values.Select(v => $"{v.Number} = {v.Name}"));
        var names = new OpenApiArray();
        names.AddRange(values.Select(v => new OpenApiString(v.Name)));
        schema.Extensions["x-enum-varnames"] = names;
    }
}
