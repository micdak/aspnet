using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PDFSharpMerge.AppFrameWorks
{
    public static class SwaggerConfiguration
    {
        public static void ConfigureSwagger(this IServiceCollection services)
        {
            services.AddSwaggerGen(opt =>
            {
                opt.SwaggerDoc("v1", new Info
                {
                    Title = "Coherent PDf services",
                    Version = "v1",
                    Description = "A system for filling pdf forms",
                    TermsOfService = "None",
                });

                var basePath = Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, "PDFSharpMerge.xml");

                opt.IncludeXmlComments(string.Format(basePath));
            });

            services.ConfigureSwaggerGen(opt =>
            {

                opt.OperationFilter<FileUploadOperation>(); // Register file upload operation filter for uploading questions files

            });

            services.ConfigureSwaggerGen(opt =>
            {
                opt.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>>
                {
                    { "Bearer", new string[] { } }
                });

                opt.AddSecurityDefinition("Bearer", new ApiKeyScheme()
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                    Name = "Authorization",
                    In = "header",
                    Type = "apiKey"
                });
            });
        }

        public static void UseSwaggerConfig(this IApplicationBuilder app)
        {
            app.UseSwagger();

            // Enable middleware to serve swagger-ui assets (HTML, JS, CSS etc.)
            app.UseSwaggerUI(x =>
            {
                x.SwaggerEndpoint("/swagger/v1/swagger.json", "PDF Merge API");
            });
        }
    }

    public class FileUploadOperation : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            if (operation.OperationId.ToLower() == "apipdfmergeuploadlistfieldspost")
            {
                operation.Parameters.Clear();
                operation.Parameters.Add(new NonBodyParameter
                {
                    Name = "file",
                    In = "formData",
                    Description = "Upload File",
                    Required = false,
                    Type = "file"
                });
                operation.Parameters.Add(new NonBodyParameter
                {
                    Name = "json",
                    In = "formData",
                    Description = "Json format of file",
                    Required = false,
                    Type = "string"
                });
                operation.Consumes.Add("application/x-www-form-urlencoded");
            }

            if (operation.OperationId.ToLower() == "apipdfmergeuploadfillpdfpost")
            {
                operation.Parameters.Clear();
                operation.Parameters.Add(new NonBodyParameter
                {
                    Name = "file",
                    In = "formData",
                    Description = "Upload File",
                    Required = false,
                    Type = "file"
                });
                operation.Parameters.Add(new NonBodyParameter
                {
                    Name = "forvaluesjson",
                    In = "formData",
                    Description = "values of form fields to be filled",
                    Required = true,
                    Type = "string"
                });
                operation.Parameters.Add(new NonBodyParameter
                {
                    Name = "json",
                    In = "formData",
                    Description = "Json format of file",
                    Required = false,
                    Type = "string"
                });
                operation.Consumes.Add("application/x-www-form-urlencoded");
                operation.Produces = new[] { "application/octet-stream" };
                operation.Responses["200"].Schema = new Schema { Type = "file", Format = "binary", };
            }
        }
    }
}
