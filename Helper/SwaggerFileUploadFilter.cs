using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;
using System.Collections.Generic;

public class SwaggerFileUploadFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var fileParam = context.MethodInfo.GetParameters()
            .FirstOrDefault(p => p.ParameterType.GetProperties()
                .Any(prop => prop.PropertyType == typeof(IFormFile)));

        if (fileParam != null)
        {
            var schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["file"] = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    }
                },
                Required = new HashSet<string> { "file" }
            };

            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = schema
                    }
                }
            };

            // Clear existing parameters to avoid duplicate/conflicting Swagger entries
            operation.Parameters.Clear();
        }
    }
}
