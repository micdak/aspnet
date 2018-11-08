using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.IO;
using PDFSharpMerge.Classes;
using MessagingToolkit.Barcode;
using System.IO.Compression;
using System.Net.Http;
using Coherent.Docstore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PDFSharpMerge.Controllers {
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class pdfmergeController : Controller {

        private IHostingEnvironment _hostingEnvironment;
        private DocstoreClient _docstoreClient;

        public pdfmergeController(IHostingEnvironment hostingEnvironment, DocstoreClient docstoreClient) {
            _hostingEnvironment = hostingEnvironment;
            _docstoreClient = docstoreClient;
        }

        public class JSONPDFFillPost {
            public Dictionary<string, string> FormFields { get; set; }
            public string Base64PDF { get; set; }
        }

        public class JSONStoredPDFFillPost {
            public Dictionary<string, string> FormFields { get; set; }
            public string FormName { get; set; }
        }

        public class JSONPDFListPost {
            public string Base64PDF { get; set; }
        }

        [HttpGet("listfields"), DisableRequestSizeLimit]
        public ActionResult listfieldsGet() {
            //string form = System.IO.File.ReadAllText(_hostingEnvironment.ContentRootPath + "/StaticContent/listfields.html");
            string form = @"<!DOCTYPE html>
<html>
<body>
    <h2>Get Fields in a PDF</h2>
    <br />
    Use the form below to get the a properly formatted JSON string for fillng the file.  Then go to <a href=""fillpdf"">./fillpdf</a>.
    <br />
    <h2>Doc Store</h2>
    <form method=""post"" enctype=""multipart/form-data"">
        <table border=""1"">
            <tr>
                <td align=""right"">File Path: <br /></td>
                <td><input name=""filePath"" type=""text"" /></td>
            </tr>
             <tr>
                <td align=""right"">token: <br /></td>
                <td><input name=""token"" type=""text"" /></td>
            </tr>
            <tr>
                <td colspan=""2"" align=""center""><input name=""action"" type=""submit"" value=""Scan for Fields"" formaction=""listfields""></td>
            </tr>
        </table>
        <p />
    </form>
     <br />
      <hr/>
       <h2>Upload File</h2>
    <form method=""post"" enctype=""multipart/form-data"">
        <table border=""1"">
            <tr>
                <td align=""right"">File: <br /></td>
                <td><input name=""UploadFileName"" type=""file"" /></td>
            </tr>
            <tr>
                <td colspan=""2"" align=""center""><input name=""action"" type=""submit"" value=""Scan for Fields (Using Mulitpart Post)"" formaction=""upload/listfields""></td>
            </tr>
        </table>
        <p />
    </form>
   <br />
    <br />
<form method=""post"" enctype='application/json'>
        <table border=""1"">
            <tr>
                <td align=""right"">json:<br /><button type='button' id='GenerateJSON'>Generate JSON</button> <br />(Will be generated from the Fields above. <br /> Will be slow in browser because textarea performance is poor with large strings <br />Is not a problem for server code)</td>
                <td><textarea autocomplete=""off"" autocorrect=""off"" autocapitalize=""off"" spellcheck=""false"" name=""json"" rows=""10"" cols=""60""></textarea></td>
            </tr>
            <tr>
                <td colspan=""2"" align=""center""><input name=""action"" type=""submit"" value=""Scan for Fields (Using Full json Post)"" formaction=""upload/listfields""> </td>
            </tr>
        </table>
        NOTE: For server calls, may also put this JSON string directly into the request body and POST it to /api/pdfmerge/upload/listfields with ""application/json"" encoding.
        <p />
    </form>
    
    <br />
    <script>
        document.getElementById('GenerateJSON').addEventListener('click', function () {
            var files = document.getElementsByName('UploadFileName')[0].files;
            if (files.length > 0) {
                var reader = new FileReader();
                reader.readAsDataURL(files[0]);
                reader.onload = function () {
                    document.getElementsByName('json')[0].value = '{""Base64PDF"": ""' + reader.result.substring(reader.result.indexOf("","") + 1) + '""}';
                };
                reader.onerror = function (error) {
                    console.log('Error: ', error);
                };
            }
        });


    </script>
</body>
</html>";

            return Content(form, "text/html", Encoding.Unicode);


        }

        [HttpPost("jlistfields"), DisableRequestSizeLimit, Produces("application/json")]
        public ActionResult jlistfieldsPost([FromBody] JSONPDFFillPost JPost) {
            try {
                byte[] data = Convert.FromBase64String(JPost.Base64PDF);
                var stream = new MemoryStream(data, 0, data.Length);
                return Json(GetFieldsFromPDF(PdfReader.Open(stream)));
            } catch(System.Exception ex) {
                return Json("Error: " + ex.Message);
            }
        }

        [HttpPost("upload/listfields"), DisableRequestSizeLimit]
        public ActionResult GetUploadFileFields(string json = "")
        {
            try
            {
                PdfDocument PDFDocument = null;

                if (Request.Form.Files.Count > 0 && Request.Form.Files[0].Length > 0)
                {
                    var file = Request.Form.Files[0];
                    string fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
                    byte[] memPDF = Helper.ReadFully(file.OpenReadStream(), 0);   // critical to grab a full memory copy - or else performance will be terrible!

                    PDFDocument = PdfReader.Open(new MemoryStream(memPDF));
                }
                else if (json != "")
                {
                    JSONPDFFillPost jsonPost = JsonConvert.DeserializeObject<JSONPDFFillPost>(json);
                    byte[] data = Convert.FromBase64String(jsonPost.Base64PDF);
                    var stream = new MemoryStream(data, 0, data.Length);
                    PDFDocument = PdfReader.Open(stream);
                }


                return Json(GetFieldsFromPDF(PDFDocument));
            }
            catch (System.Exception ex)
            {
                return Json("Error: " + ex.Message);
            }
        }


        [HttpPost("listfields"), Produces("application/json")]
        public async Task<ActionResult> listfieldsPost(string token, string filePath, string json = "" )
        {
            try {
                var fileparts =  Helper.GetPathAndName(filePath);
                PdfDocument PDFDocument = null;
                var response = await _docstoreClient.GetFileContent(fileparts.Item2, fileparts.Item1);

                PDFDocument = PdfReader.Open(new MemoryStream(response));

                if(json != "") { 
                    JSONPDFFillPost jsonPost = JsonConvert.DeserializeObject<JSONPDFFillPost>(json);
                    byte[] data = Convert.FromBase64String(jsonPost.Base64PDF);
                    var stream = new MemoryStream(data, 0, data.Length);
                    PDFDocument = PdfReader.Open(stream);
                }


                return Json(GetFieldsFromPDF(PDFDocument));
            } catch(System.Exception ex) {
                return Json("Error: " + ex.Message);
            }
        }

        private static Dictionary<string, string> GetFieldsFromPDF(PdfDocument FormDocument) {
            Dictionary<string, string> dictPDFFields = new Dictionary<string, string>();
            if(FormDocument == null || FormDocument.AcroForm == null)
                throw new Exception("No PDF with fillable form submitted");


            PdfAcroForm af = FormDocument.AcroForm;

            foreach (string key in af.Fields.Names)
            {
                try { 
                var field = af.Fields[key];
                if (field is PdfTextField)
                {
                    dictPDFFields.Add(key, ((PdfTextField)field).Text);
                }
                else if (field is PdfCheckBoxField)
                {
                    dictPDFFields.Add(key, ((PdfCheckBoxField)field).Checked.ToString());
                }
                else if (field is PdfSignatureField)
                {
                    dictPDFFields.Add(key, "");  //Leave empty since we just do image filling
                }
                else if (field is PdfComboBoxField)
                {

                        if (((PdfComboBoxField)field).Value is PdfString)
                            dictPDFFields.Add(key, (((PdfComboBoxField)field).Value as PdfString).Value);
                        else
                            dictPDFFields.Add(key, ((PdfComboBoxField)field).Value?.ToString());
                }
                else if (field is PdfRadioButtonField)
                {
                    dictPDFFields.Add(key, ((PdfRadioButtonField)field).Value?.ToString());
                }
                else if (field is PdfListBoxField)
                {
                    PdfListBoxField pl = (PdfListBoxField)field;
                        string values = "";
                        if(pl.Value is PdfArray) {
                            PdfSharp.Pdf.PdfArray pa = (PdfSharp.Pdf.PdfArray)pl.Value;

                            foreach (PdfString pi in pa.Elements.Items)
                            {
                                if (values.Length > 0) values += ",";
                                values += pi.Value;
                            }
                        } else {
                            values = pl.Value.ToString();
                        }
                        dictPDFFields.Add(key, values);
                    } else {
                        dictPDFFields.Add(key, "Unsupported Type");
                    }
                } catch(Exception e) {
                    throw new Exception("Scan of Field '" + key + " failed.  " + e.Message);
                }
            }

            return (dictPDFFields);
        }

        [HttpGet("fillpdf"), DisableRequestSizeLimit]
        public ActionResult FillPDFGet() {
            //string form = System.IO.File.ReadAllText(_hostingEnvironment.ContentRootPath + "/StaticContent/fillpdf.html");   //this may be causing problems with AWS
            string form = @"<!DOCTYPE html>
<html>
<body>
    <h2>Fill Fields in a PDF</h2>
    <br />
    To get a list of fields and corresonding JSON sample please use <a href=""listfields"">./listfields</a>.
    <br />
    This NEW version handles CJK field fills by completely overwriting the field with PDF text if a Chinese font is encountered
    <br />
    This fixes Chrome problems but is still experimental.  Formatting may not work correctly.  Non Chinese characters will still use
    <br />
    Standard form filling though.  If you notice errors please try fillng with stanard ASCII only and send both samples to Peter.
    <br />
    <br />
    Signature Fields expect to be pouplated via a Base64 Encoded PNG image.
    <br />
	Text Fields ending in ""_QRFill"" will have their value encoded as a QR code and overlaid (make sure you set up the field to be the right size).  
    <br />
    Also suported are ""_Code39Fill"" (numbers only) and ""_PDF417Fill"" barcodes. Others can easily be implemented on request. 
    <br />
    Post to this URL to call the API.  You can see the structure by hitting view source on this page.
    <br />
0<h2>Doc Store</h2>
    <form method=""post"">
        <table border=""1"">
            <tr>
                <td align=""right"">File Path: <br /></td>
                <td><input name=""filePath"" type=""text"" /></td>
            </tr>
            <tr>
                <td align=""right"">Token: <br /></td>
                <td><input name=""token"" type=""text"" /></td>
            </tr>
            <tr>
                <td align=""right"">Name/Value JSON</td>
                <td><textarea name=""forvaluesjson"" rows=""10"" cols=""60""></textarea></td>
            </tr>
            <tr>
                <td colspan=""2"" align=""center""><input name=""action"" type=""submit"" value=""Fill PDF "" formaction=""fillpdf""></td>
            </tr>
        </table>
        <p />
    </form>
<br />
    <hr />
<br />
<h2>Upload file</h2>
<form method=""post"" enctype=""multipart/form-data"">
        <table border=""1"">
            <tr>
                <td align=""right"">File: <br /></td>
                <td><input name=""UploadFileName"" type=""file"" /></td>
            </tr>
            <tr>
                <td align=""right"">Name/Value JSON</td>
                <td><textarea name=""formvaluesjson"" rows=""10"" cols=""60""></textarea></td>
            </tr>
            <tr>
                <td colspan=""2"" align=""center""><input name=""action"" type=""submit"" value=""Fill PDF (Using MultiPart Post)"" formaction=""upload/fillpdf""></td>
            </tr>
        </table>
        <p />
    </form>
<br />
 <form method=""post"" enctype='application/json'>
        <table border=""1"">
            <tr>
                <td align=""right"">JSON:<br /><button type='button' id='GenerateJSON'>Generate JSON</button> <br />(Will be generated from the Fields above. <br /> Will be slow in browser because textarea performance is poor with large strings <br />Is not a problem for server code)</td>
                <td><textarea autocomplete=""off"" autocorrect=""off"" autocapitalize=""off"" spellcheck=""false"" name=""json"" rows=""10"" cols=""60""></textarea></td>
            </tr>
            <tr>
                <td colspan=""2"" align=""center""><input name=""action"" type=""submit"" value=""Fill PDF (Using full JSON Post)"" formaction=""upload/fillpdf""></td>
            </tr>
        </table>
        NOTE: For server calls, may also put this JSON string directly into the request body and POST it to /api/pdfmerge/upload/fillpdf with ""application/json"" encoding.
        <p />
    </form>
    <script>
        document.getElementById('GenerateJSON').addEventListener('click', function () {
            var files = document.getElementsByName('UploadFileName')[0].files;
            if (files.length > 0) {
                var reader = new FileReader();
                reader.readAsDataURL(files[0]);
                reader.onload = function () {
                    document.getElementsByName('json')[0].value = '{  ""FormFields"": ' + document.getElementsByName('formvaluesjson')[0].value + ' , ""Base64PDF"": ""' + reader.result.substring(reader.result.indexOf("","") + 1) + '""}';
                };
                reader.onerror = function (error) {
                    console.log('Error: ', error);
                };
            }
        });


    </script>
</body>
</html>";
            return Content(form, "text/html", Encoding.Unicode);
        }

        [HttpPost("jfillpdf"), DisableRequestSizeLimit]
        public ActionResult jFillPDFPost([FromBody] JSONPDFFillPost JPost) {
            try {
                byte[] data = Convert.FromBase64String(JPost.Base64PDF);
                var stream = new MemoryStream(data, 0, data.Length);
                var PDFDocument = PdfReader.Open(stream);

                FillPDF(JPost.FormFields, PDFDocument);

                var fsr = new MemoryStream();
                PDFDocument.Flatten();
                PDFDocument.Save(fsr);
                return new FileStreamResult(fsr, "application/pdf");
            } catch(System.Exception ex) {
                return new StatusCodeResult(400); // Json("Fill failed: " + ex.Message);
            }

        }

        [HttpPost("upload/fillpdf"), DisableRequestSizeLimit]
        public ActionResult FileUploadPdfFile(string formvaluesjson = "", string json = "")
        {
            try
            {
                PdfDocument PDFDocument = null;
                JSONPDFFillPost jsonPost = null;
                Dictionary<string, string> submittedFields = null;
                if (Request.Form.Files.Count > 0 && Request.Form.Files[0].Length > 0)
                {
                    string fileName = ContentDispositionHeaderValue.Parse(Request.Form.Files[0].ContentDisposition).FileName.Trim('"');

                    byte[] memPDF = Helper.ReadFully(Request.Form.Files[0].OpenReadStream(), 0);   // critical to grab a full memory copy - or else performance will be terrible!
                    PDFDocument = PdfReader.Open(new MemoryStream(memPDF));
                }
                else if (json != "")
                {
                    jsonPost = JsonConvert.DeserializeObject<JSONPDFFillPost>(json);
                    byte[] data = Convert.FromBase64String(jsonPost.Base64PDF);
                    var stream = new MemoryStream(data, 0, data.Length);
                    PDFDocument = PdfReader.Open(stream);
                }

                if (formvaluesjson != "")
                    submittedFields = JsonConvert.DeserializeObject<Dictionary<string, string>>(formvaluesjson);
                else
                    submittedFields = jsonPost.FormFields;

                FillPDF(submittedFields, PDFDocument);

                var fsr = new MemoryStream();
                PDFDocument.Flatten();
                PDFDocument.Save(fsr);
                return new FileStreamResult(fsr, "application/pdf");

            }
            catch (System.Exception ex)
            {
                return Json("Fill failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="forvaluesjson"></param>
        /// <param name="json"></param>
        /// <returns>FileStreamResult</returns>
        [HttpPost("fillpdf"), DisableRequestSizeLimit]
        public async Task<ActionResult> FillPDFPost(string token, string filePath, string forvaluesjson = "", string json = "")
        {

            try {
                PdfDocument PDFDocument = null;
                JSONPDFFillPost jsonPost = null;
                Dictionary<string, string> submittedFields = null;
                var fileParts = Helper.GetPathAndName(filePath);
                var fileResponse  = await _docstoreClient.GetFileContent(fileParts.Item2, fileParts.Item1);
                
                PDFDocument = PdfReader.Open(new MemoryStream(fileResponse));
                if(json != "") {
                    jsonPost = JsonConvert.DeserializeObject<JSONPDFFillPost>(json);
                    byte[] data = Convert.FromBase64String(jsonPost.Base64PDF);
                    var stream = new MemoryStream(data, 0, data.Length);
                    PDFDocument = PdfReader.Open(stream);
                }

                if(forvaluesjson != "")
                    submittedFields = JsonConvert.DeserializeObject<Dictionary<string, string>>(forvaluesjson);
                else
                    submittedFields = jsonPost.FormFields;

                FillPDF(submittedFields, PDFDocument);
                var fsr = new MemoryStream();
                PDFDocument.Flatten();
                PDFDocument.Save(fsr);
                var contents  = fsr.ToArray();
                var response = await _docstoreClient.UpsertDocument(fileParts.Item2, fileParts.Item1, contents, new Dictionary<string, string>());
                return Json(response.Response.Url);

            } catch(System.Exception ex) {
                return Json("Fill failed: " + ex.Message);
            }
        }

        [HttpPost("storepdfform"), DisableRequestSizeLimit]
        public ActionResult StorePDFForm(IFormFile formFile, string formName) {
            try {
                string filePath = Path.Combine(AppContext.BaseDirectory, "PDFForms");

                if(!Directory.Exists(filePath)) {
                    Directory.CreateDirectory(filePath);
                }

                if(formName.Contains(".")) {
                    formName = formName.Replace(".", "_");
                }

                filePath = Path.Combine(filePath, formName + ".pdf");

                using(Stream stream = formFile.OpenReadStream()) {
                    var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                    stream.CopyTo(fileStream);
                    fileStream.Dispose();
                }

                return Json(new { Message = "Sucessfully Store", FormName = formName, StatusCode = "SUCCESS" });
            } catch(Exception ex) {
                return Json(new { Message = "Error generate while storing pdf form", ExceptionMessage = ex.Message, StakeTrace = ex.StackTrace, StatusCode = "ERROR" });
            }

        }

        [HttpPost("jfillstoredpdf"), DisableRequestSizeLimit]
        public ActionResult jFillStoredPDFPost([FromBody]JSONStoredPDFFillPost Jpost) {
            try {
                if(Jpost == null) {
                    return Json(new { Message = "Empty Data Passed .", StatusCode = "ERROR" });
                }

                if(string.IsNullOrEmpty(Jpost.FormName)) {
                    return Json(new { Message = "Empty Form Name .", StatusCode = "ERROR" });
                }

                if(Jpost.FormFields == null) {
                    return Json(new { Message = "Empty Form Data .", StatusCode = "ERROR" });
                }

                if(Jpost.FormName.Contains(".")) {
                    Jpost.FormName = Jpost.FormName.Replace(".", "_");
                }

                string filePath = Path.Combine(AppContext.BaseDirectory, "PDFForms", Jpost.FormName + ".pdf");
                if(!System.IO.File.Exists(filePath)) {
                    return Json(new { Message = "pdf form does not exist.", StatusCode = "ERROR" });
                }
                byte[] data = System.IO.File.ReadAllBytes(filePath);
                var PDFDocument = PdfReader.Open(new MemoryStream(data));

                FillPDF(Jpost.FormFields, PDFDocument);

                var fsr = new MemoryStream();
                PDFDocument.Flatten();
                PDFDocument.Save(fsr);
                return new FileStreamResult(fsr, "application/pdf");
            } catch(System.Exception ex) {
                return new StatusCodeResult(400); // Json("Fill failed: " + ex.Message);
            }

        }

        [HttpPost("mergestoredpdf"), DisableRequestSizeLimit]
        public ActionResult mergeStoredPDFPost([FromBody]List<JSONStoredPDFFillPost> Jposts) {
            string processId = Guid.NewGuid().ToString();
            string folderPath = Path.Combine(AppContext.BaseDirectory, "GeneratedPDFForms", processId);

            bool hasSucess = true;
            StringBuilder errorMessage = new StringBuilder();
            if(Jposts == null) {
                hasSucess = false;
                errorMessage = errorMessage.AppendLine("Empty Data Passed .");
            }

            foreach(var item in Jposts) {
                try {
                    if(hasSucess) {
                        if(string.IsNullOrEmpty(item.FormName)) {
                            hasSucess = false;
                            errorMessage = errorMessage.AppendLine("Empty Form Name.");
                        }

                        if(item.FormFields == null) {
                            hasSucess = false;
                            errorMessage = errorMessage.AppendLine("Empty Form Data. Form Name: " + item.FormName);
                        }

                        if(item.FormName.Contains(".")) {
                            item.FormName = item.FormName.Replace(".", "_");
                        }

                        string filePath = Path.Combine(AppContext.BaseDirectory, "PDFForms", item.FormName + ".pdf");
                        if(!System.IO.File.Exists(filePath)) {
                            hasSucess = false;
                            errorMessage = errorMessage.AppendLine("Pdf Form does not exist. Form Name: " + item.FormName);
                        }

                        byte[] memPDF = System.IO.File.ReadAllBytes(filePath);   // critical to grab a full memory copy - or else performance will be terrible!
                        PdfDocument PDFDocument = PdfReader.Open(new MemoryStream(memPDF));

                        FillPDF(item.FormFields, PDFDocument);

                        if(!System.IO.Directory.Exists(folderPath)) {
                            Directory.CreateDirectory(folderPath);
                        }

                        string generatedfilePath = Path.Combine(folderPath, item.FormName + ".pdf");
                        PDFDocument.Save(generatedfilePath);
                    }
                } catch(System.Exception ex) {
                    hasSucess = false;
                    errorMessage = errorMessage.AppendLine("Error generate while processing form - Form Name: " + item.FormName + " Error Message:" + ex.Message);
                }
            }

            if(hasSucess) {
                string zipfolderPath = Path.Combine(AppContext.BaseDirectory, "ZipFiles");
                if(!System.IO.Directory.Exists(zipfolderPath)) {
                    Directory.CreateDirectory(zipfolderPath);
                }

                IEnumerable<string> files = Directory.EnumerateFiles(folderPath);
                if(files.Count() > 1) {
                    string zipPath = Path.Combine(zipfolderPath, Guid.NewGuid().ToString() + ".zip");
                    using(FileStream zipToOpen = new FileStream(zipPath, FileMode.CreateNew)) {
                        using(ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create)) {
                            foreach(var file in files) {
                                archive.CreateEntryFromFile(file, Path.GetFileName(file));
                            }
                        }
                    }

                    return new FileStreamResult(System.IO.File.OpenRead(zipPath), "application/zip");
                } else if(files.Count() > 0) {
                    return new FileStreamResult(System.IO.File.OpenRead(files.FirstOrDefault()), "application/pdf");
                } else {
                    var fsr = new MemoryStream(Encoding.UTF8.GetBytes("Error occured while pdf generates"));
                    return new FileStreamResult(fsr, "text/plain");
                }
            } else {
                var fsr = new MemoryStream(Encoding.UTF8.GetBytes(errorMessage.ToString()));
                return new FileStreamResult(fsr, "text/plain");
            }
        }

        private static void FillPDF(Dictionary<string, string> submittedFields, PdfDocument PDFDocument) {
            if(PDFDocument == null || PDFDocument.AcroForm == null)
                throw new Exception("No PDF with fillable form submitted");

            PdfAcroForm af = PDFDocument.AcroForm;


            //make sure fields are enabled
            if(af.Elements.ContainsKey(PdfAcroForm.Keys.NeedAppearances)) {
                af.Elements[PdfAcroForm.Keys.NeedAppearances] = new PdfSharp.Pdf.PdfBoolean(true);
            } else {
                af.Elements.Add(PdfAcroForm.Keys.NeedAppearances, new PdfSharp.Pdf.PdfBoolean(true));
            }


            //Turn on unicode support
            XPdfFontOptions options = new XPdfFontOptions(PdfFontEncoding.Unicode);

            //foreach (PDFField sField in submittedFields)
            foreach(KeyValuePair<string, string> sField in submittedFields) {
                try {
                    var field = af.Fields[sField.Key];
                    if(field is PdfTextField && (!sField.Key.EndsWith("_QRFill") && !sField.Key.EndsWith("_Code39Fill") && !sField.Key.EndsWith("_PDF417Fill"))) {
                        // filling in a text field
                        PdfTextField tf = (PdfTextField)field;

                        tf.ReadOnly = false;
                        if(Helper.IsChinese(sField.Value))  //override font if chinese is detected.
                            if(true)  // remove evenatully - was used for switching different rendering methods
                            {
                                //Note: This will work for text Fields.. but make sure they are not linked (ie. multiple fileds with same name)
                                // If you want to make this more generic for all form fields you need to account for the possilbility
                                // that a form field could have several phyical outputs across multiple pages.  You can find the same 
                                // properties but you need to check the KIDS collection for the field.  When there is only one display
                                // for a form field then the properties are rolled up to the form field itself - so the below works.
                                PdfRectangle rect = field.Elements.GetRectangle("/Rect");

                                if(tf.HasKids) throw new Exception("Multiple render locations found for text field" + sField.Key + ".  This can be supported, or just create separate fields for now.");

                                tf.Value = new PdfSharp.Pdf.PdfString(sField.Value, PdfStringEncoding.Unicode);

                                //also a bit of pain to locate the acutaly page the field is on... but OK.
                                XGraphics gfx = null;
                                foreach(var page in PDFDocument.Pages) {
                                    if(page.Reference == field.Elements["/P"]) {
                                        if(gfx != null)   //dispose of object before getting a new one.  Should actually never happen... but better safe than sorry....
                                            gfx.Dispose();
                                        gfx = XGraphics.FromPdfPage(page);
                                        break;
                                    }
                                }


                                XFont targetFont = new XFont("MicrosoftJhengHei", tf.Font.Size, XFontStyle.Regular, options);   //It's "KaigenSansSC" behind the scenes, but this may help with some rendering....
                                // Draw the text over the field                      
                                gfx.DrawString(sField.Value, targetFont, XBrushes.Black, new XRect(Math.Min(rect.X1,rect.X2), gfx.PageSize.Height - Math.Min(rect.Y1,rect.Y2) - Math.Abs(rect.Height), Math.Abs(rect.Width), Math.Abs(rect.Height)), XStringFormats.TopLeft);
                                gfx.Dispose();

                                //Theb hide the field
                                if(tf.Elements.ContainsKey("/F") == true)
                                    tf.Elements.SetInteger("/F", 6);


                                //original filling routine.. still kept here for refernce ... and since it will allow extracting form values from the PDF later.
                                tf.Font = targetFont;   // this is and the below is necessary to set both the view and edit font of the field.

                                if(tf.Elements.ContainsKey("/DA") == false) {
                                    tf.Elements.Add(PdfTextField.Keys.DA, new PdfString($"/{targetFont.Name} {targetFont.Size} Tf 0 g"));
                                } else {
                                    tf.Elements[PdfTextField.Keys.DA] = new PdfString($"/{targetFont.Name} {targetFont.Size} Tf 0 g");
                                }
                            }
                        tf.Value = new PdfSharp.Pdf.PdfString(sField.Value, PdfStringEncoding.Unicode);
                    }
                    else if (field is PdfCheckBoxField)
                    {
                        if (sField.Value != null && sField.Value.Equals("TRUE", StringComparison.InvariantCultureIgnoreCase))
                            ((PdfCheckBoxField)field).Checked = true;
                        else
                            ((PdfCheckBoxField)field).Checked = false;

                    }
                    else if (field is PdfComboBoxField)
                    {
                        ((PdfComboBoxField)field).Value = new PdfString(sField.Value);
                    } else if(field is PdfRadioButtonField) {
                        ((PdfRadioButtonField)field).Value = new PdfName(sField.Value);

                    }
                    else if (field is PdfListBoxField)
                    {
                        string[] items = sField.Value.Split(",");
                        //PdfSharp.Pdf.PdfArray paNew = new PdfSharp.Pdf.PdfArray();
                        PdfArray pas = (PdfArray)(((PdfListBoxField)field).Value);
                        //PdfItem[] pis = pas.Elements.Items;
                        pas.Elements.Clear();
                        foreach (string sitem in items)

                        {
                            pas.Elements.Add(new PdfString(sitem));
                        }


                        //PdfListBoxField pl = (PdfListBoxField)field;
                        //PdfSharp.Pdf.PdfArray pa = (PdfSharp.Pdf.PdfArray)pl.Value;
                        //string values = "";
                        //foreach (PdfString pi in pa.Elements.Items)
                        //{
                        //    if (values.Length > 0) values += ", ";
                        //    values += pi.Value;
                        //}
                        //dictPDFFields.Add(key, values);
                    } else if(field is PdfSignatureField) {
                        if(sField.Value == "") continue;

                        //Note: The below wroks because a named singature fields only appear once in a document.
                        // If you want to make this more generic for all form fields you need to account for the possilbility
                        // that a form field could have several phyical outputs across multiple pages.  You can find the same 
                        // properties but you need to check the KIDS collection for the field.  When there is only one display
                        // for a form field then the properties are rolled up to the form field itself - so the below works.
                        PdfSignatureField sig = (PdfSignatureField)field;
                        OverlayImageOnField(PDFDocument, sField.Value, sig.Elements.GetRectangle("/Rect"), sig.Elements["/P"]);

                    } else if(field is PdfTextField && (sField.Key.EndsWith("_QRFill") || sField.Key.EndsWith("_Code39Fill") || sField.Key.EndsWith("_PDF417Fill"))) {
                        if(sField.Value == "") continue;

                        //Note: This will work for text Fields.. but make sure they are not linked (ie. multiple fileds with same name)
                        // If you want to make this more generic for all form fields you need to account for the possilbility
                        // that a form field could have several phyical outputs across multiple pages.  You can find the same 
                        // properties but you need to check the KIDS collection for the field.  When there is only one display
                        // for a form field then the properties are rolled up to the form field itself - so the below works.
                        PdfRectangle rect = field.Elements.GetRectangle("/Rect");

                        PdfTextField tf = (PdfTextField)field;

                        if(tf.HasKids) throw new Exception("Multiple render locations found on barcode fill.  This can be supported, or just create separate fields for now.");

                        tf.Value = new PdfSharp.Pdf.PdfString(sField.Value, PdfStringEncoding.Unicode);

                        //This hides the field
                        if(tf.Elements.ContainsKey("/F") == true)
                            tf.Elements.SetInteger("/F", 6);

                        MessagingToolkit.Barcode.BarcodeEncoder benc = new BarcodeEncoder();
                        benc.Width = Convert.ToInt32((rect.X2 - rect.X1) * 5);  //I just made up that rule - but seems to work well for laser printers. 
                        benc.Height = Convert.ToInt32((rect.Y2 - rect.Y1) * 5);

                        if(sField.Key.EndsWith("_Code39Fill")) {
                            benc.Encode(BarcodeFormat.Code39, sField.Value);
                        } else if(sField.Key.EndsWith("_PDF417Fill")) {
                            benc.Encode(BarcodeFormat.PDF417, sField.Value);
                        } else  //_QRFill
                          {
                            benc.ErrorCorrectionLevel = MessagingToolkit.Barcode.QRCode.Decoder.ErrorCorrectionLevel.M;  //M=15%  H=25% L=7% (default)
                            benc.Encode(BarcodeFormat.QRCode, sField.Value);
                        }

                        byte[] qrCodeAsPngByteArr = benc.GetImageData(SaveOptions.Png);
                        OverlayImageOnField(PDFDocument, qrCodeAsPngByteArr, rect, field.Elements["/P"]);



                    }

                } catch(Exception e) {
                    throw new Exception("Fill of Field '" + sField.Key + "' ('" + sField.Value + "') failed.  " + e.Message);
                }
            }
        }
        /// <summary>
        /// Draws an image over a rectange (like those specified in a form field)
        /// </summary>
        /// <param name="PDFDocument"></param>
        /// <param name="b64Image">Should work for PNG, JPG, GIF and BMP</param>
        /// <param name="rect"></param>
        /// <param name="focusPageReference"></param>
        private static void OverlayImageOnField(PdfDocument PDFDocument, string b64Image, PdfRectangle rect, PdfItem focusPageReference) {
            byte[] data = Convert.FromBase64String(b64Image);
            //byte[] data = Helper.GetJohnHancockPNGB64();

            OverlayImageOnField(PDFDocument, data, rect, focusPageReference);
        }


        /// <summary>
        /// Draws an image over a rectange (like those specified in a form field)
        /// </summary>
        /// <param name="PDFDocument"></param>
        /// <param name="sImage">Should work for PNG, JPG, GIF and BMP</param>
        /// <param name="rect"></param>
        /// <param name="focusPageReference"></param>
        private static void OverlayImageOnField(PdfDocument PDFDocument, byte[] image, PdfRectangle rect, PdfItem focusPageReference) {

            var stream = new MemoryStream(image, 0, image.Length);


            //also a bit of pain to locate the acutaly page the field is on... but OK.
            XGraphics gfxObj = null;
            foreach(var page in PDFDocument.Pages) {
                if(page.Reference == focusPageReference) {
                    if(gfxObj != null)   //dispose of object before getting a new one.  Should actually never happen... but better safe than sorry....
                        gfxObj.Dispose();
                    gfxObj = XGraphics.FromPdfPage(page);
                    break;
                }
            }

            // Draw the image


            XImage ximage = XImage.FromStream(stream);

            double xscaling = 1;
            double yscaling = 1;
            if(ximage.PointWidth / ximage.PointHeight > rect.Width / rect.Height)
                yscaling = (ximage.PointWidth / ximage.PointHeight) / (rect.Width / rect.Height);
            else
                xscaling = (rect.Width / rect.Height) / (ximage.PointWidth / ximage.PointHeight);

            double scaling = Math.Max(ximage.PointHeight / rect.Height, ximage.PointWidth / rect.Width);
            if(gfxObj.PageDirection == XPageDirection.Downwards) {
                gfxObj.DrawImage(ximage, rect.X1, gfxObj.PageSize.Height - rect.Y1 - rect.Height, rect.Width / xscaling, rect.Height / yscaling);
            } else {
                gfxObj.DrawImage(ximage, rect.X1, rect.Y1, rect.Width / scaling, rect.Height / scaling);
            }
            gfxObj.Dispose();
        }
        
    }

}
