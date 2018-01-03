using Microsoft.ApplicationInsights;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace HelloAzure.Controllers
{
    public class HomeController : Controller
    {
        private TelemetryClient telemetry = new TelemetryClient();
        private Scanner.Scanner scanner = Scanner.Scanner.Instance;

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            //ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        [HttpPost]  
        public ActionResult About(HttpPostedFileBase file)  
        {  
            try  
            {  
                if (file.ContentLength > 0)  
                {  
                    string _FileName = Path.GetFileName(file.FileName);
                    var path = Server.MapPath("~/UploadedFiles");
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    string uploadedPath = Path.Combine(path, _FileName);

                    file.SaveAs(uploadedPath);
                    if (scanner.ScanFile(uploadedPath))
                    {
                        ViewBag.Message = "File Uploaded Successfully!!";
                        telemetry.TrackEvent(ViewBag.Message);
                    }
                    else
                    {
                        ViewBag.Message = "File Scanning failed!!";
                        telemetry.TrackEvent(ViewBag.Message);
                    }
                } 

                return View();
            }  
            catch  (Exception e)
            {  
                ViewBag.Message = "File upload failed!! " + e.Message;
                telemetry.TrackEvent(ViewBag.Message);

                return View();  
            }  
        }  
    }
}