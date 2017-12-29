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
                    string uploadedPath = Path.Combine(Server.MapPath("~/UploadedFiles"), _FileName);
                    string scannedPath = Path.Combine(Server.MapPath("~/ScannedFiles"), _FileName);

                    file.SaveAs(uploadedPath);
                    scanner.ScanFile(uploadedPath, scannedPath);
                }  
                ViewBag.Message = "File Uploaded Successfully!!";
                telemetry.TrackEvent(ViewBag.Message);

                return View();
            }  
            catch  
            {  
                ViewBag.Message = "File upload failed!!";
                telemetry.TrackEvent(ViewBag.Message);

                return View();  
            }  
        }  
    }
}