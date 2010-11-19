using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotRas;
using System.Net;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;
using System.ComponentModel;

namespace VpnRouteClient
{
    class Program
    {
        static private DotRas.RasPhoneBook AllUsersPhoneBook=new RasPhoneBook();
        static private DotRas.RasDialer Dialer=new DotRas.RasDialer();

        static void Main(string[] args)
        {
            //Do some UAC checks
            WindowsPrincipal pricipal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            bool hasAdministrativeRight = pricipal.IsInRole(WindowsBuiltInRole.Administrator);
            if (!hasAdministrativeRight)
            {
                RunElevated(Process.GetCurrentProcess().MainModule.FileName);
                return;
            }
            // This opens the phonebook so it can be used. Different overloads here will determine where the phonebook is opened/created.
            AllUsersPhoneBook.Open();

            string EntryName = System.Configuration.ConfigurationManager.AppSettings["DialupName"];

            if(!string.IsNullOrEmpty(System.Configuration.ConfigurationManager.AppSettings["host"]))
            {
                // Create the entry that will be used by the dialer to dial the connection. Entries can be created manually, however the static methods on
                // the RasEntry class shown below contain default information matching that what is set by Windows for each platform.
                RasEntry entry = RasEntry.CreateVpnEntry(EntryName, System.Configuration.ConfigurationManager.AppSettings["host"], RasVpnStrategy.PptpFirst,
                    RasDevice.GetDeviceByName("(PPTP)", RasDeviceType.Vpn));

            
                // Add the new entry to the phone book.
                try
                {
                    AllUsersPhoneBook.Entries.Add(entry);
                }
                catch (System.ArgumentException err)
                {
                    int x = 0;
                    //Most likely, already exists.  Continue on and try connection.
                }
            }
            Dialer.EntryName = EntryName;
            Dialer.PhoneBookPath = RasPhoneBook.GetPhoneBookPath(RasPhoneBookType.AllUsers);
            Dialer.StateChanged += new EventHandler<StateChangedEventArgs>(Dialer_StateChanged);
            Dialer.DialCompleted += new EventHandler<DialCompletedEventArgs>(Dialer_DialCompleted);

            try
            {
                if(string.IsNullOrEmpty(System.Configuration.ConfigurationManager.AppSettings["User"]))
                    Dialer.AllowUseStoredCredentials = true;
                else
                {
                // Set the credentials the dialer should use.
                Dialer.Credentials = new NetworkCredential(System.Configuration.ConfigurationManager.AppSettings["User"], 
                    System.Configuration.ConfigurationManager.AppSettings["pass"]);
                }

                // NOTE: The entry MUST be in the phone book before the connection can be dialed.
                
                Dialer.Dial();
                

               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return;
            }

            foreach (string routeLine in System.Configuration.ConfigurationManager.AppSettings["route"].Split(";".ToCharArray(),StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = routeLine.Split(" ".ToCharArray());
                AddRoute(parts[0], parts[1], parts[2]);
            }
        


            
        }
        private static void RunElevated(string fileName)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo();
            processInfo.Verb = "runas";
            processInfo.FileName = fileName;
            try
            {
                Process.Start(processInfo);
            }
            catch (Win32Exception)
            {
                //Do nothing. Probably the user canceled the UAC window
            }
        }
        static void Dialer_DialCompleted(object sender, DialCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                Console.WriteLine("Cancelled!");
            }
            else if (e.TimedOut)
            {
                Console.WriteLine("Connection attempt timed out!");
            }
            else if (e.Error != null)
            {
                Console.WriteLine(e.Error.ToString());
            }
            else if (e.Connected)
            {
                Console.WriteLine("Connection successful!");
            }

            if (!e.Connected)
            {
                // The connection was not connected, disable the disconnect button.
               Console.WriteLine("Connected!");
            }
            
        }

        static void Dialer_StateChanged(object sender, StateChangedEventArgs e)
        {
            Console.WriteLine(e.State.ToString());
        }

        private static void AddRoute(string ip, string mask, string route)
        {
            Process p = new Process();

            p.StartInfo.UseShellExecute = false;

            p.StartInfo.FileName = "route";

            p.StartInfo.Arguments = "-p add " + ip + " mask " + mask + " " + route;

            //p.StartInfo.RedirectStandardOutput = true;

            //p.StartInfo.StandardOutputEncoding = Encoding.ASCII;

            p.OutputDataReceived += new DataReceivedEventHandler(p_OutputDataReceived);

            p.Start();

            

            p.WaitForExit();
        }

        static void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }
    }
}
