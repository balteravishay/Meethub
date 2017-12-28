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
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        [HttpPost]  
        public ActionResult UploadFile(HttpPostedFileBase file)  
        {  
            try  
            {  
                if (file.ContentLength > 0)  
                {  
                    string _FileName = Path.GetFileName(file.FileName);  
                    string _path = Path.Combine(Server.MapPath("~/UploadedFiles"), _FileName);  
                    file.SaveAs(_path);  
                }  
                ViewBag.Message = "File Uploaded Successfully!!";  
                return View();  
            }  
            catch  
            {  
                ViewBag.Message = "File upload failed!!";  
                return View();  
            }  
        }  
    }
}