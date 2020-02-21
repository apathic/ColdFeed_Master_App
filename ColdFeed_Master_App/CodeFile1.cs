using System;
using System.Net;
using System.Net.Mail;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Data.SqlClient;
using Excel = Microsoft;

namespace ColdfeedApplications
{
    class Program
    {
        static void Main(string[] args)
        {
            string Version = "TEST";

            Move_Ultrasound_Images_to_Coldfeed(Version);
            //Thread.Sleep(60000);
            //Move_Ultrasound_Images_to_Coldfeed(Version);
            //Thread.Sleep(60000);
            //Move_Ultrasound_Images_to_Coldfeed(Version);
            //Thread.Sleep(60000);
            //Move_Ultrasound_Images_to_Coldfeed(Version);


            //xxx("TEST");

            System.Environment.Exit(1);
        }

        public static class Globals
        {
            public static string AlertEmailAddress = "TMosier@etch.com";
            public static string AlertEmailAddressCC = "BKEstep@etch.com";
        }
        
        static void Move_Ultrasound_Images_to_Coldfeed(string Version)
        {
            // \\NAS\AIMSArchive\export

            //\\NAS\shares\AIMSArchive\export

            //ID:   aimsarchive
            //PW:   _ETCH!SaveTheData!_
            //Path: \\NAS\AIMSArchive
            try
            {
                string loc = @"\\NAS\AIMSArchive\export";
                string bac = @"\\NAS\AIMSArchive\export\Backup\";
                string des = "";
                string useFolder;

                switch (Version.ToUpper())
                {
                    case "TEST":
                        des = @"\\eat-scaarch01\AIMSTest61\";
                        break;

                    case "LIVE":
                        des = @"\\eat-scaarch01\AIMSLive\";
                        break;
                }

                FixShortenedFileNames(loc);

                string[] folders = System.IO.Directory.GetDirectories(loc, "*", System.IO.SearchOption.TopDirectoryOnly);

                foreach (string f in folders)
                {
                    Thread.Sleep(60000);

                    useFolder = f.Replace(loc, "");
                    useFolder = useFolder.Replace("\\", "");

                    if (IsFolderValid(useFolder, f, Version) == true)
                    {
                        string[] filePaths = Directory.GetFiles(f, "*.jpg");

                        foreach (string s in filePaths)
                        {
                            //Filename formatting:
                            string useTime = DateTime.Now.ToString("hhmm", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
                            string useDate = DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
                            string ApplicationID = (useFolder + useDate + useTime).PadRight(30);
                            string MedAccountNum = useFolder.PadRight(12);
                            string MedUnitNum = "".PadRight(11);
                            string FormID = "USPHOTO".PadRight(15);
                            string PatientDOB = "".PadRight(7);
                            string PatientSex = " "; //M, F, U
                            string destFileName = (ApplicationID + MedAccountNum + MedUnitNum + FormID + " 00000" + PatientDOB + PatientSex + useDate + useTime + ".jpg").ToUpper();

                            WriteLogFile(useTime);
                            WriteLogFile(s + " | " + bac + destFileName);
                            File.Copy(s, bac + destFileName, true);
                            WriteLogFile(s + " | " + des + destFileName);
                            File.Copy(s, des + destFileName, true);
                            WriteLogFile(s + " | Delete");
                            File.Delete(s);
                            WriteLogFile("PostDelete");
                            if (File.Exists(des + destFileName) == true)
                            {
                                WriteLogFile("AIMS_Archive Success File Copy:" + s + " TO " + des + destFileName);
                            }
                            else
                            {                                
                                Email("Ultrasound_Coldfeed_Failure@etch.com", Globals.AlertEmailAddress, "* POTENTIAL FILE COPY FAILURE * AIMS_Ultrasound:" + s + "TO" + des + destFileName, "");
                                    
                                WriteLogFile("* POTENTIAL FILE COPY FAILURE * AIMS_Archive:" + s + "TO " + des + destFileName);
                            }

                            Thread.Sleep(2);
                        }

                        Directory.Delete(f, true);
                    }
                }
            }
            catch
            {
                WriteLogFile("Something broke in Move_Ultrasound_Images_to_Coldfeed at " + DateTime.Now.ToString("hhmmssffff", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                
                if (Version != "TEST")
                {
                    Email("Ultrasound_Coldfeed_Failure@etch.com", Globals.AlertEmailAddress, "Something broke in Move_Ultrasound_Images_to_Coldfeed at " + DateTime.Now.ToString("hhmmssffff", System.Globalization.CultureInfo.GetCultureInfo("en-US")), "");
                }
            }
        }

        static void FixShortenedFileNames(string path)
        {
            string[] folders = System.IO.Directory.GetDirectories(path, "*", System.IO.SearchOption.AllDirectories);

            foreach (string f in folders)
            {
                string folderName = f.Replace(path, "");
                folderName = folderName.Replace("\\", "");
                string temp = folderName;

                if (folderName.Length < 10 && folderName.Substring(0, 1).ToUpper() == "E")
                {
                    temp = temp.Replace("E", "");
                    temp = temp.Replace("e", "");
                    temp = temp.PadLeft(9, '0');
                    temp = "E" + temp;

                    string destFolderPath = f.Replace(folderName, "") + temp;
                    Directory.Move(f, destFolderPath.ToUpper());
                }
            }
        }

        static bool IsFolderValid(string folderName, string fullFolderPath, string Version)
        {
            try
            {
                folderName = folderName.ToUpper();
                string db = "Livefdb";

                if (folderName.Length == 10 && folderName.Substring(0, 1) == "E")
                {
                    {
                        switch (Version)
                        {
                            case "LIVE":
                                db = "Livefdb";
                                break;

                            case "TEST":
                                db = "Testfdb";
                                break;
                        }

                        using (SqlConnection connection = new SqlConnection(@"Data Source=192.168.37.36;Initial Catalog=" + db + ";User ID=rptuser;Password=rpt#200905"))
                        using (SqlCommand cmd = new SqlCommand("SELECT AccountNumber RegFormID FROM RegAcct_Main WHERE AccountNumber = '" + folderName + "'", connection))
                        {
                            connection.Open();
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    return true;
                                }
                                else
                                {
                                    Email("Ultrasound_Coldfeed_Failure@etch.com", Globals.AlertEmailAddress, "USPHOTO Ultrasound Format Failure", folderName + " is not a valid account number.");
                                    System.IO.Directory.Move(fullFolderPath, "\\\\NAS\\AIMSArchive\\export\\ToFix\\" + folderName);
                                    return false;
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (folderName.StartsWith("E"))
                    {
                        Email("Ultrasound_Coldfeed_Failure@etch.com", Globals.AlertEmailAddress, "USPHOTO Ultrasound Format Failure", folderName + " appears to be in an incorrect format. Please fix.");
                        System.IO.Directory.Move(fullFolderPath, "\\\\NAS\\AIMSArchive\\export\\ToFix\\" + folderName);
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        static void WriteLogFile(string logline)
        {
            try
            {
                StreamWriter log;
                FileStream fileStream = null;
                DirectoryInfo logDirInfo = null;
                FileInfo logFileInfo;

                string logFilePath = @"\\NAS\AIMSArchive\export\Logfiles\";

                logFilePath = logFilePath + "Log -" + System.DateTime.Today.ToString("MMddyyyy") + "." + "txt";
                logFileInfo = new FileInfo(logFilePath);
                logDirInfo = new DirectoryInfo(logFileInfo.DirectoryName);

                if (!logDirInfo.Exists) logDirInfo.Create();

                if (!logFileInfo.Exists)
                {
                    fileStream = logFileInfo.Create();
                }
                else
                {
                    fileStream = new FileStream(logFilePath, FileMode.Append);
                }

                log = new StreamWriter(fileStream);
                log.WriteLine(logline);
                log.Close();

                Garbageman(@"\\NAS\AIMSArchive\export\Logfiles\", 180);
            }
            catch
            { }
        }

        public static void Email(string MessageSenderEmailAddress, string DestinationEmails, string SubjectLine, string MessageToSend)
        {
            try
            {
                MailMessage message = new MailMessage();
                SmtpClient smtp = new SmtpClient();

                message.From = new MailAddress(Globals.AlertEmailAddress);
                message.To.Add(new MailAddress(DestinationEmails));
                message.CC.Add(new MailAddress(Globals.AlertEmailAddressCC));
                message.Subject = SubjectLine;
                message.IsBodyHtml = false;
                message.Body = MessageToSend;
                smtp.Port = 25;
                smtp.Host = "webmail.etch.com";
                smtp.EnableSsl = false;
                smtp.UseDefaultCredentials = false; 
                smtp.Send(message);
            }
            catch (Exception)
            {
                WriteLogFile("Email failure from Brian's Coldfeed app from: " + MessageSenderEmailAddress);
            }
        }

        static void Garbageman(string folderToClean, int DaysToPreserve)
        {
            string[] filePaths = Directory.GetFiles(folderToClean, "*.*");

            foreach (string s in filePaths)
            {
                DateTime Then = Convert.ToDateTime(File.GetLastWriteTime(s));
                DateTime Now = DateTime.Now;
                var age = (Now - Then).TotalDays;

                if (age > DaysToPreserve)
                {
                     Console.WriteLine(age);
                    File.Delete(s);
                }
                else
                {
                    Console.WriteLine(age);
                }
            }
        }

        static void xxx(string Version)
        {
            string sourceDir = "";
            string destDir = "";
            string backupDir = "";
            string logfileDir = "";

            //AIMSFORM-N00000-70174824-14269-20200203112000-E042002360-70179840.pdf
            string docType = "";        //AIMSFORM
            string N00000 = "";         //N00000
            string formID = "";         //70174824
            string Junk = "";           //14269
            string docDateTime = "";    //20200203112000
            string accountNum = "";     //E042002360
            string Not_Applicable = ""; //70179840

            switch (Version.ToUpper())
            { 
                case "LIVE":
                    sourceDir = "";
                    destDir = "";
                    backupDir = "";
                    logfileDir = "";                     
                    break;

                case "TEST":
                    sourceDir = @"C:\Test";
                    destDir = @"C:\Test";
                    backupDir = @"C:\Test";
                    logfileDir = @"C:\Logfiles";
                    break;
            }

            //Copy to mprint server for redistribution using aims names


            string[] filePaths = Directory.GetFiles(sourceDir, "*.pdf");

            foreach (string s in filePaths)
            {
                string[] a = s.Split('-');
                //AIMSFORM-N00000-70174824-14269-20200203112000-E042002360-70179840.pdf

                docType = a[0];
                N00000 = a[1];
                formID = a[2];
                Junk = a[3];
                docDateTime = a[4];
                accountNum = a[5];
                Not_Applicable = a[6];

                //Mve to mprint for redistribution using aims names


                //Filename formatting:
                string useTime = DateTime.Now.ToString("hhmm", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
                string useDate = docDateTime; // = DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
                string ApplicationID = (accountNum + useDate + useTime).PadRight(30);
                string MedAccountNum = accountNum.PadRight(12);
                string MedUnitNum = "".PadRight(11);
                string FormID = docType.PadRight(15);
                string PatientDOB = "".PadRight(7);
                string PatientSex = " "; //M, F, U
                string destFileName = (ApplicationID + MedAccountNum + MedUnitNum + FormID + " 00000" + PatientDOB + PatientSex + useDate + useTime + ".pdf").ToUpper();

                //File.Copy(s, backupDir + destFileName);
                //File.Copy(s, destDir + destFileName);
                //File.Delete(s);

                if (File.Exists(destDir + destFileName) == true)
                {
                    WriteLogFile("AIMS_Archive Success File Copy:" + s + " TO " + destDir + destFileName);
                }
                else
                {
                    WriteLogFile("* POTENTIAL FILE COPY FAILURE * AIMS_Archive:" + s + "TO " + destDir + destFileName);
                    //Email("Ultrasound_Coldfeed_Failure@etch.com", Globals.AlertEmailAddress, "* POTENTIAL FILE COPY FAILURE * AIMS_Ultrasound:" + s + "TO" + des + destFileName, "");
                }

                Thread.Sleep(2);

            }
        }

        static void AIMS_Coldfeed()
        {
            string SourceFile, filename, Form, accountnumber, junk, urn, flag, stringpart, docyear, strResults, first, second, third;
            int x, y, z;

            //strResults = Dir(@"\\192.168.38.241\Coldfeed\*.pdf");
            //SourceFile = @"\\192.168.38.241\Coldfeed\";

            string[] filePaths = Directory.GetFiles(@"\\192.168.38.241\Coldfeed", "*.pdf");

            foreach (string s in filePaths)
            {


            }
        }
    }    
}