using Coherent.Docstore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PDFSharpMerge.AppFrameWorks;
using PDFSharpMerge.Classes;
using System.Text;

namespace PDFSharpMerge
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Classes.MyFontResolver.Apply();  //necessary to do this before another font resolver gets loaded... under .net core you can't seem to reset the global resolver.
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddMvc().AddJsonOptions(options =>
            {
                options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
            });
            services.AddTransient((serviceProvider) => {
                IHttpContextAccessor httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
                string token = httpContextAccessor.HttpContext.Request.Headers["Authorization"];
                if (!string.IsNullOrEmpty(token))
                {
                    token = token.Replace("Bearer ", "");
                }
                else
                {
                    if (httpContextAccessor.HttpContext.Request.Query.ContainsKey("token"))
                    {
                        token = httpContextAccessor.HttpContext.Request.Query["token"].ToString();
                    }
                }
                return new DocstoreClient(token, Config.DocStoreUrl, new LoggerFactory());
            });
            
            services.ConfigureSwagger();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            app.UseSwaggerConfig();
            app.UseMvc();
        }
    }

    
}
