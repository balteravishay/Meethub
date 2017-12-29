using Microsoft.ApplicationInsights;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace HelloAzure.Scanner
{
    class Scanner
    {
        static Scanner _instance;

        public static Scanner Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Scanner();
                return _instance;
            }

        }

        string[] _scan_rules = null;
        private static TelemetryClient telemetry = new TelemetryClient();

        private Scanner()
        {
            var url = System.Configuration.ConfigurationManager.AppSettings["OPS_URL"];
            var token = System.Configuration.ConfigurationManager.AppSettings["OPS_TOKEN"];
            telemetry.TrackTrace($"OPSWAT URL: {url} | OPSWAT KEY: {token}");
            // set the Metadefender REST base URL.
            OPSWAT_Integration.REST_URL = url;
            OPSWAT_Integration.API_TOKEN = token;
            try
            {
                // get the Metadefender core policy rules (timeout is set to 30 seconds)
                _scan_rules = OPSWAT_Integration.GetRules(30);
            }
            catch (Exception ex)
            {
                HandleError("Get server rules", ex);
                return;
            }

            // print the rules retrieved by the server
            telemetry.TrackTrace(string.Format("Server rules:"));
            foreach (string rule in _scan_rules)
                telemetry.TrackTrace(string.Format("\t{0}", rule));
            
        }


        public bool ScanFile(string uploadPath, string scannedPath)
        {
            bool response = false;
            // report the screen
            telemetry.TrackTrace(string.Format("\n\n> Processing {0}...", uploadPath));

            // init objects for scan results
            string blocked_reason = null;
            System.IO.MemoryStream output_file = null;
            try
            {
                // process the file and get the scan results, block reason (in a case the file being blocked) and the sanitized stream of the file (in a case the file is being sanitized)
                // please note that in this sample, the utility will always send the file to the first policy rule on Metadefender.
                // you can change it as you like (scan_rules[0])
                if (OPSWAT_Integration.isShouldBlock(null, uploadPath, _scan_rules[0], 60, out blocked_reason, out output_file))
                {
                    // results are true
                    telemetry.TrackTrace(string.Format(" >> File should be BLOCKED!"));

                    // print the block reason
                    if (!string.IsNullOrEmpty(blocked_reason))
                        telemetry.TrackTrace(string.Format(" >>> Reason: {0}", blocked_reason));
                    response = false;
                }

                else
                {
                    // results are false
                    telemetry.TrackTrace(string.Format(" >> File is allowed"));

                    // file is sanitized
                    if (output_file != null)
                    {
                        // report the screen
                        telemetry.TrackTrace(string.Format("> Saving sanitized file to {0}", scannedPath));

                        // create the file
                        System.IO.FileStream fstream = System.IO.File.Create(scannedPath);

                        // write the memory stream to the file
                        output_file.WriteTo(fstream);

                        // close and dispose the stream and file objects
                        output_file.Close();
                        fstream.Close();

                        output_file.Dispose();
                        fstream.Dispose();

                        response = true;
                    }

                    telemetry.TrackTrace(string.Format("\n - Process completed -"));
                }
            }
            catch (Exception ex)
            {
                HandleError("File scan", ex);
                response = false;
            }
            return response;
        }

        #region Error Handler
        private static void HandleError(string eventName, Exception ex)
        {
            if (ex is WebException)
            {
                WebException wex = ex as WebException;
                HttpWebResponse res = ((HttpWebResponse)(wex.Response));

                if (res != null)
                {
                    string serverMessage = null;

                    System.IO.StreamReader respStream = new System.IO.StreamReader(res.GetResponseStream());
                    if (respStream != null)
                    {
                        try
                        {
                            serverMessage = respStream.ReadToEnd();

                            serverMessage = serverMessage.Trim();
                            if ((serverMessage.StartsWith("{") && serverMessage.EndsWith("}")) || //For object
                                (serverMessage.StartsWith("[") && serverMessage.EndsWith("]"))) //For array
                            {
                                Dictionary<string, object> deserializedJsonDictionary;

                                deserializedJsonDictionary = (Dictionary<string, object>)new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(serverMessage);
                                serverMessage = (string)deserializedJsonDictionary["err"];
                            }
                            else
                            {
                                serverMessage = null;
                            }

                            respStream.Dispose();
                        }
                        catch (Exception jex)
                        {
                            //Exception in parsing json
                            telemetry.TrackTrace(string.Format("Failed to parse json string from error message: {0}", jex.Message));
                        }
                    }

                    telemetry.TrackTrace(string.Format("An error occurred on {0} event: HTTP/{1} {2}", eventName, (int)res.StatusCode, serverMessage == null ? res.StatusDescription : serverMessage));
                }
                else
                {
                    telemetry.TrackTrace(string.Format("An error occurred on {0} event: {1}", eventName, wex.Message));
                }
            }
            else
            {
                telemetry.TrackTrace(string.Format("An error occurred on {0} event: {1}", eventName, ex.Message));
            }
          
        }
        #endregion
    }
}