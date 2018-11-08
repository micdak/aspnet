using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PDFSharpMerge.Classes
{
    public static class Config
    {
        public static string DocStoreUrl = Environment.GetEnvironmentVariable("DOCSTORE_URL");
    }
}
