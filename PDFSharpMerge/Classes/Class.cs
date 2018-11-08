using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PdfSharp.Fonts;
using System.Reflection;
using System.IO;

namespace PDFSharpMerge.Classes
{

    class MyFontResolver : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Ignore case of font names.
            var name = familyName.ToLower().TrimEnd('#');

            // Deal with the fonts we know.
            switch (name)
            {
                case "kaigensanssc":
                    return new FontResolverInfo("KaigenSansSC#");
                case "microsoftjhenghei":
                    return new FontResolverInfo("KaigenSansSC#");
            }

            // We pass all other font requests to the default handler.
            // When running on a web server without sufficient permission, you can return a default font at this stage.
            return PlatformFontResolver.ResolveTypeface(familyName, isBold, isItalic);
        }

        /// <summary>
        /// Return the font data for the fonts.
        /// </summary>
        public byte[] GetFont(string faceName)
        {
            switch (faceName)
            {
                case "KaigenSansSC#":
                    return FontHelper.KaigenSansSC;

                case "KaigenSansSC#b":
                    return FontHelper.KaigenSansSC;

                case "KaigenSansSC#i":
                    return FontHelper.KaigenSansSC;

                case "KaigenSansSC#bi":
                    return FontHelper.KaigenSansSC;

                case "MicrosoftJhengHei#":
                    return FontHelper.KaigenSansSC;

                case "MicrosoftJhengHei#b":
                    return FontHelper.KaigenSansSC;

                case "MicrosoftJhengHei#i":
                    return FontHelper.KaigenSansSC;

                case "MicrosoftJhengHei#bi":
                    return FontHelper.KaigenSansSC;
            }

            return null;
        }


        internal static MyFontResolver OurGlobalFontResolver = null;

        /// <summary>
        /// Ensure the font resolver is only applied once (or an exception is thrown)
        /// </summary>
        internal static void Apply()
        {

            //TODO: Possible Bug - setting this under .net core does not work the same as .net standard.
            if (OurGlobalFontResolver == null)
                OurGlobalFontResolver = new MyFontResolver();
            if (GlobalFontSettings.FontResolver == null)
                GlobalFontSettings.FontResolver = OurGlobalFontResolver;

        }
    }


    /// <summary>
    /// Helper class that reads font data from embedded resources.
    /// </summary>
    public static class FontHelper
    {
        public static byte[] KaigenSansSC
        {
            get { return LoadFontData("Doesnt Matter"); }
        }
        public static byte[] MicrosoftJhengHei
        {
            get { return LoadFontData("Doesnt Matter"); }
        }



        /// <summary>
        /// Returns the specified font from an embedded resource.
        /// </summary>
        static byte[] LoadFontData(string name)
        {

            // Test code to find the names of embedded fonts
            var assembly = Assembly.GetEntryAssembly();

            using (Stream stream = assembly.GetManifestResourceStream("PDFSharpMerge.Fonts.KaigenSansSC-Regular.ttf"))
            {
                if (stream == null)
                    throw new ArgumentException("No resource with name Fonts\\KaigenSansSC-Regular.ttf");

                int count = (int)stream.Length;
                byte[] data = new byte[count];
                stream.Read(data, 0, count);
                return data;
            }
        }
    }
}
