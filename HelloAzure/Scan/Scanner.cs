using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace HelloAzure.Scanner
{
    class Scan
    {
        string[] _scan_rules = null;

        public Scan(string url, string token)
        {
            // set the Metadefender REST base URL.
            OPSWAT_Integration.REST_URL = rest_url;
            
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
            Console.WriteLine("Server rules:");
            foreach (string rule in scan_rules)
                Console.WriteLine("\t{0}", rule);

            // report the screen
            Console.WriteLine("\n\n> Processing {0}...", filepath);

        }


        public void Scan(string filepath)
        {
            // init objects for scan results
            string blocked_reason = null;
            System.IO.MemoryStream output_file = null;

            try
            {
                // process the file and get the scan results, block reason (in a case the file being blocked) and the sanitized stream of the file (in a case the file is being sanitized)
                // please note that in this sample, the utility will always send the file to the first policy rule on Metadefender.
                // you can change it as you like (scan_rules[0])
                if (OPSWAT_Integration.isShouldBlock(null, filepath, _scan_rules[0], 60, out blocked_reason, out output_file))
                {
                    // results are true
                    Console.WriteLine(" >> File should be BLOCKED!");

                    // print the block reason
                    if (!string.IsNullOrEmpty(blocked_reason))
                        Console.WriteLine(" >>> Reason: {0}", blocked_reason);
                }

                else
                {
                    // results are false
                    Console.WriteLine(" >> File is allowed");

                    // file is sanitized
                    if (output_file != null)
                    {
                        // report the screen
                        Console.WriteLine("> Saving sanitized file on desktop");

                        // clean file will be saved on your desktop or anywhere else you will set it
                        string saveInPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
                            System.IO.Path.GetFileName (filepath));

                        // create the file
                        System.IO.FileStream fstream = System.IO.File.Create(saveInPath);

                        // write the memory stream to the file
                        output_file.WriteTo(fstream);

                        // close and dispose the stream and file objects
                        output_file.Close();
                        fstream.Close();

                        output_file.Dispose();
                        fstream.Dispose();
                    }

                    Console.WriteLine("\n - Process completed -");
                }
            }
            catch (Exception ex)
            {
                HandleError("File scan", ex);
            }

        }
    }
}