﻿using System;
using CloudCoinCore;
using CloudCoinClient.CoreClasses;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using Founders;
using Microsoft.Extensions.CommandLineUtils;
using System.Collections.Generic;
using CloudCoinCoreDirectory;
using System.Net;
using Newtonsoft.Json;
using Microsoft.Extensions;
using Microsoft.Extensions.Configuration;
using Celebrium;
using ZXing;
using QRCoder;
using System.Drawing;

namespace Founders_2._0
{
    class Program
    {
        public static IConfiguration Configuration { get; set; }
        public static KeyboardReader reader = new KeyboardReader();
        public static String rootFolder = Directory.GetCurrentDirectory();
        static FileSystem FS = new FileSystem(rootFolder);
        public static RAIDA raida;
        //static List<RAIDA> networks = new List<RAIDA>();
        public static String prompt = "> ";
        public static Frack_Fixer fixer;
        public static String[] commandsAvailable = new String[] { "Echo raida", "Show CloudCoins in Bank", "Import / Pown & Deposit", "Export / Withdraw", "Fix Fracked", "Show Folders", "Export stack files with one note each", "Help", "Quit" };
        public static int NetworkNumber = 1;
        public static SimpleLogger logger = new SimpleLogger(FS.LogsFolder + "logs" + DateTime.Now.ToString("yyyyMMdd").ToLower() + ".log", true);
        static TrustedTradeSocket tts;

        #region Total Variables
        public static int onesCount = 0;
        public static int fivesCount = 0;
        public static int qtrCount = 0;
        public static int hundredsCount = 0;
        public static int twoFiftiesCount = 0;

        public static int onesFrackedCount = 0;
        public static int fivesFrackedCount = 0;
        public static int qtrFrackedCount = 0;
        public static int hundredsFrackedCount = 0;
        public static int twoFrackedFiftiesCount = 0;

        public static int onesTotalCount = 0;
        public static int fivesTotalCount = 0;
        public static int qtrTotalCount = 0;
        public static int hundredsTotalCount = 0;
        public static int twoFiftiesTotalCount = 0;
        #endregion


        static public int DisplayMenu()
        {
            Console.WriteLine("Founders Actions                  Secret Word:" + tts.secretWord);
            Console.WriteLine();
            Console.WriteLine("1. Echo RAIDA");
            Console.WriteLine("2. Show CloudCoins");
            Console.WriteLine("3. Import CloudCoins");
            Console.WriteLine("4. Export CloudCoins");
            Console.WriteLine("5. Fix Fracked Coins");
            Console.WriteLine("6. Show Folders");
            Console.WriteLine("7. Help");
            Console.WriteLine("8. Switch Network");
            Console.WriteLine("9. Send Coins Over Trusted Trade");
            Console.WriteLine("10. Exit");
            var result = Console.ReadLine();
            return Convert.ToInt32(result);
        }
        public class AppSettings
        {
            public string Hello { get; set; }
        }
        public static void initConfig()
        {
            var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json");

            Configuration = builder.Build();
            string nn = Configuration["NetworkNumber"];

            try
            {
                NetworkNumber = Convert.ToInt16(nn);
            }
            catch(Exception e)
            {
                NetworkNumber = 1;
                updateLog("Reading Network Number Config failed. Setting default to 1.");
            }
            
            //services.Configure<AppSettings>(appSettings);
        }

        
        public static void SetupRAIDA()
        {
            RAIDA.FileSystem = new FileSystem(rootFolder);
            try
            {
                RAIDA.Instantiate();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                Environment.Exit(1);
            }
            if (RAIDA.networks.Count == 0)
            {
                updateLog("No Valid Network found.Quitting!!");
                Environment.Exit(1);
            }
            else
            {
                updateLog(RAIDA.networks.Count + " Networks found.");
                raida = (from x in RAIDA.networks
                         where x.NetworkNumber == NetworkNumber
                         select x).FirstOrDefault();
                raida.FS = FS;
                RAIDA.ActiveRAIDA = raida;
                if (raida == null)
                {
                    updateLog("Selected Network Number not found. Quitting.");
                    Environment.Exit(0);
                }
                else
                {
                    updateLog("Network Number set to " + NetworkNumber);
                }
            }
            //networks[0]
        }

        public async static Task SwitchNetwork(int NewNetworkNumber){
            int oldRAIDANumber = NetworkNumber;
            RAIDA oldRAIDA = raida;
            NetworkNumber = NewNetworkNumber;
            raida = (from x in RAIDA.networks
                     where x.NetworkNumber == NetworkNumber
                     select x).FirstOrDefault();
            if (raida == null)
            {
                updateLog("Selected Network Number not found. Reverting to  previous network.");
                raida = oldRAIDA;
            }
            else
            {
                updateLog("Network Number set to " + NetworkNumber);
                await EchoRaida();
            }
        }

        public static void Main(params string[] args)
        {
            Setup();
            initConfig();
            updateLog("Loading Network Directory");
            SetupRAIDA();
            FS.LoadFileSystem();
            RAIDA.logger = logger;
            fixer = new Frack_Fixer(FS, Config.milliSecondsToTimeOut);

            Console.Clear();
            // Program.exe <-g|--greeting|-$ <greeting>> [name <fullname>]
            // [-?|-h|--help] [-u|--uppercase]
            #region CommandLineArguments
            CommandLineApplication commandLineApplication =
              new CommandLineApplication(throwOnUnexpectedArg: false);
            CommandArgument names = null;
            commandLineApplication.Command("name",
              (target) =>
                names = target.Argument(
                  "fullname",
                  "Enter the full name of the person to be greeted.",
                  multipleValues: true));
            CommandOption greeting = commandLineApplication.Option(
              "-$|-g |--greeting <greeting>",
              "The greeting to display. The greeting supports"
              + " a format string where {fullname} will be "
              + "substituted with the full name.",
              CommandOptionType.NoValue);
            CommandOption uppercase = commandLineApplication.Option(
              "-u | --uppercase", "Display the greeting in uppercase.",
              CommandOptionType.NoValue);
            commandLineApplication.HelpOption("-? | -h | --help");

            CommandOption echo = commandLineApplication.Option(
              "-$|-e |--echo ",
              "The greeting to display. The greeting supports"
              + " a format string where {fullname} will be "
              + "substituted with the full name.",
                CommandOptionType.NoValue);

            CommandOption total = commandLineApplication.Option(
            "-$|-b |--bank ",
            "Shows details of your coins in bank.",
            CommandOptionType.NoValue);

            CommandOption backup = commandLineApplication.Option(
            "-$|-ba |--backup ",
            "Backup your coins to specified folder.",
            CommandOptionType.SingleValue);


            CommandOption folders = commandLineApplication.Option(
              "-$|-f |--folders ",
              "The command to display CloudCoin Working Folder Structure",
                CommandOptionType.NoValue);

            CommandOption pown = commandLineApplication.Option(
              "-$|-p |--pown ",
              "The command to pown/detect/import your CloudCoins.",
                CommandOptionType.NoValue);

            CommandOption detection = commandLineApplication.Option(
              "-$|-d |--detect ",
              "The command to pown/detect/import your CloudCoins.",
                CommandOptionType.NoValue);

            CommandOption import = commandLineApplication.Option(
              "-$|-i |--import ",
              "The command to pown/detect/import your CloudCoins.",
                CommandOptionType.NoValue);

            #endregion

            if (args.Length <1)
            {
                printWelcome();
                while (true)
                {
                    try
                    {
                        int input = DisplayMenu();
                        ProcessInput(input).Wait();
                        if (input == 10)
                            break;
                    }
                    catch(Exception e)
                    {
                        break;
                    }
                }
            }
            else
            {

            commandLineApplication.OnExecute(async () =>
            {
                if (echo.HasValue())
                {
                    //ech();
                    await EchoRaida();
                }
                if(folders.HasValue())
                {
                    ShowFolders();
                }

                if (pown.HasValue() || detection.HasValue() || import.HasValue())
                {
                    await RAIDA.ProcessNetworkCoins(NetworkNumber);
                }
                if (greeting.HasValue())
                {
                    printWelcome();
                }
                if(total.HasValue())
                {
                    showCoins();
                }
                if (backup.HasValue())
                {
                    Console.WriteLine(backup.Value());
                }
                return 0;
            });
            commandLineApplication.Execute(args);
            }

        }

        private static void showCoins()
        {
            Console.Out.WriteLine("");
            // This is for consol apps.
            Banker bank = new Banker(FS);
            int[] bankTotals = bank.countCoins(FS.BankFolder);
            int[] frackedTotals = bank.countCoins(FS.FrackedFolder);
            // int[] counterfeitTotals = bank.countCoins( counterfeitFolder );

            var bankCoins = FS.LoadFolderCoins(FS.BankFolder);

            
            onesCount = (from x in bankCoins
                         where x.denomination == 1
                         select x).Count();
            fivesCount = (from x in bankCoins
                          where x.denomination == 5
                          select x).Count();
            qtrCount = (from x in bankCoins
                        where x.denomination == 25
                        select x).Count();
            hundredsCount = (from x in bankCoins
                             where x.denomination == 100
                             select x).Count();
            twoFiftiesCount = (from x in bankCoins
                               where x.denomination == 250
                               select x).Count();

            var frackedCoins = FS.LoadFolderCoins(FS.FrackedFolder);
            bankCoins.AddRange(frackedCoins);

            onesFrackedCount = (from x in frackedCoins
                         where x.denomination == 1
                         select x).Count();
            fivesFrackedCount = (from x in frackedCoins
                          where x.denomination == 5
                          select x).Count();
            qtrFrackedCount = (from x in frackedCoins
                        where x.denomination == 25
                        select x).Count();
            hundredsFrackedCount = (from x in frackedCoins
                             where x.denomination == 100
                             select x).Count();
            twoFrackedFiftiesCount = (from x in frackedCoins
                               where x.denomination == 250
                               select x).Count();

            onesTotalCount = onesCount + onesFrackedCount;
            fivesTotalCount = fivesCount + fivesFrackedCount;
            qtrTotalCount = qtrCount + qtrFrackedCount;
            hundredsTotalCount = hundredsCount + hundredsFrackedCount;
            twoFiftiesTotalCount = twoFiftiesCount + twoFrackedFiftiesCount;


            int totalAmount = onesTotalCount + (fivesTotalCount * 5) + (qtrTotalCount * 25) + (hundredsTotalCount * 100) + (twoFiftiesTotalCount * 250);

            //Output  " 12.3"
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.Out.WriteLine("                                                                    ");
            Console.Out.WriteLine("    Total Coins in Bank:    " + string.Format("{0,8:N0}", totalAmount) + "                                ");
            Console.Out.WriteLine("                                                                    ");
            Console.Out.WriteLine("                 1s         5s         25s       100s       250s    ");
            Console.Out.WriteLine("                                                                    ");
            Console.Out.WriteLine("   Perfect:   " + string.Format("{0,7}", onesCount) + "    " + string.Format("{0,7}", fivesCount) + "    " + string.Format("{0,7}", qtrCount) + "    " + string.Format("{0,7}", hundredsCount) + "    " + string.Format("{0,7}", twoFiftiesCount) + "   ");
            Console.Out.WriteLine("                                                                    ");
            Console.Out.WriteLine("   Fracked:   " + string.Format("{0,7}", onesFrackedCount) + "    " + string.Format("{0,7}", fivesFrackedCount) + "    " + string.Format("{0,7}", qtrFrackedCount) + "    " + string.Format("{0,7}", hundredsFrackedCount) + "    " + string.Format("{0,7}", twoFrackedFiftiesCount) + "   ");
            Console.Out.WriteLine("                                                                    ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
        }
        private async static Task ProcessInput(int input)
        {
            switch (input)
            {
                case 1:
                    await EchoRaida();
                    break;
                case 2:
                    showCoins();
                    break;
                case 3:
                    //await detect();
                    await RAIDA.ProcessNetworkCoins(NetworkNumber);
                    break;
                case 4:
                    export();
                    break;
                case 6:
                    try
                    {
                        Process.Start(FS.RootPath);
                    }
                    catch(Exception e)
                    {
                        updateLog(e.Message);
                    }
                    ShowFolders();
                    break;
                case 5:
                    Fix();
                    //fix(timeout);
                    break;
                case 7:
                    help();
                    break;
                case 8:
                    Console.Write("Enter New Network Number - ");
                    int nn= Convert.ToInt16( Console.ReadLine());
                    await SwitchNetwork(nn);
                    break;
                case 9:
                    await SendCoinsTT();
                    break;
                default:
                    break;
            }
        }
        private static void ech(){
            Console.WriteLine("Echo...");
        }
        private static void Greet(
          string greeting, IEnumerable<string> values, bool useUppercase)
        {
            Console.WriteLine(greeting);
        }

        private static int GetNetworkNumber(RAIDADirectory dir)
        {
            return 1;

        }
        public static void printWelcome()
        {
            Console.WriteLine("");
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Out.WriteLine("                                                                  ");
            Console.Out.WriteLine("                   CloudCoin Founders Edition                     ");
            Console.Out.WriteLine("                      Version: October.10.2017                    ");
            Console.Out.WriteLine("          Used to Authenticate, Store and Payout CloudCoins       ");
            Console.Out.WriteLine("      This Software is provided as is with all faults, defects    ");
            Console.Out.WriteLine("          and errors, and without warranty of any kind.           ");
            Console.Out.WriteLine("                Free from the CloudCoin Consortium.               ");
            Console.Out.WriteLine("                            Network Number "+ NetworkNumber +"                      ");
            Console.Out.WriteLine("                                                                  ");

            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;

        } // End print welcome
        public static void help()
        {
            Console.Out.WriteLine("");
            Console.Out.WriteLine("Customer Service:");
            Console.Out.WriteLine("Open 9am to 11pm California Time(PST).");
            Console.Out.WriteLine("1 (530) 500 - 2646");
            Console.Out.WriteLine("CloudCoin.HelpDesk@gmail.com(unsecure)");
            Console.Out.WriteLine("CloudCoin.HelpDesk@Protonmail.com(secure if you get a free encrypted email account at ProtonMail.com)");

        }//End Help

        public async static Task EchoRaida()
        {
            Console.Out.WriteLine(String.Format( "Starting Echo to RAIDA Network {0}\n",NetworkNumber));
            Console.Out.WriteLine("----------------------------------\n");
            var echos = raida.GetEchoTasks();
           

            await Task.WhenAll(echos.AsParallel().Select(async task => await task()));
            Console.Out.WriteLine("Ready Count -" + raida.ReadyCount);
            Console.Out.WriteLine("Not Ready Count -" + raida.NotReadyCount);

            for (int i = 0; i < raida.nodes.Count(); i++)
            {
                Debug.WriteLine("Node" + i + " Status --" + raida.nodes[i].RAIDANodeStatus);
                //updateLog("Node" + i + " Status --" + raida.nodes[i].RAIDANodeStatus);
            }
            Console.Out.WriteLine("-----------------------------------\n");

        }

        public static void updateLog(string logLine)
        {
            logger.Info(logLine);
            Console.Out.WriteLine(logLine);
        }

        public static void export()
        {
            Console.Out.WriteLine("");
            Banker bank = new Banker(FS);
            int[] bankTotals = bank.countCoins(FS.BankFolder);
            int[] frackedTotals = bank.countCoins(FS.FrackedFolder);
            Console.Out.WriteLine("  Your Bank Inventory:");
            //int grandTotal = (bankTotals[0] + frackedTotals[0]);
            showCoins();
            // state how many 1, 5, 25, 100 and 250
            int exp_1 = 0;
            int exp_5 = 0;
            int exp_25 = 0;
            int exp_100 = 0;
            int exp_250 = 0;
            //Warn if too many coins
            Console.WriteLine(bankTotals[1] + frackedTotals[1] + bankTotals[2] + frackedTotals[2] + bankTotals[3] + frackedTotals[3] + bankTotals[4] + frackedTotals[4] + bankTotals[5] + frackedTotals[5]);
            if (((bankTotals[1] + frackedTotals[1]) + (bankTotals[2] + frackedTotals[2]) + (bankTotals[3] + frackedTotals[3]) + (bankTotals[4] + frackedTotals[4]) + (bankTotals[5] + frackedTotals[5])) > 1000)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Out.WriteLine("Warning: You have more than 1000 Notes in your bank. Stack files should not have more than 1000 Notes in them.");
                Console.Out.WriteLine("Do not export stack files with more than 1000 notes. .");
                Console.ForegroundColor = ConsoleColor.White;
            }//end if they have more than 1000 coins

            Console.Out.WriteLine("  Do you want to export your CloudCoin to (1)jpgs , (2) stack (JSON) , (3) QR Code (4) 2D Bar code file?");
            int file_type = reader.readInt(1, 4);
            // 1 jpg 2 stack
            if (onesTotalCount > 0)
            {
                Console.Out.WriteLine("  How many 1s do you want to export?");
                exp_1 = reader.readInt(0, (onesTotalCount));
            }

            // if 1s not zero 
            if (fivesTotalCount > 0)
            {
                Console.Out.WriteLine("  How many 5s do you want to export?");
                exp_5 = reader.readInt(0, (fivesTotalCount));
            }

            // if 1s not zero 
            if ((qtrTotalCount > 0))
            {
                Console.Out.WriteLine("  How many 25s do you want to export?");
                exp_25 = reader.readInt(0, (qtrTotalCount));
            }

            // if 1s not zero 
            if (hundredsTotalCount > 0)
            {
                Console.Out.WriteLine("  How many 100s do you want to export?");
                exp_100 = reader.readInt(0, (hundredsTotalCount));
            }

            // if 1s not zero 
            if (twoFiftiesTotalCount > 0)
            {
                Console.Out.WriteLine("  How many 250s do you want to export?");
                exp_250 = reader.readInt(0, (twoFiftiesTotalCount));
            }

            // if 1s not zero 
            // move to export
            Exporter exporter = new Exporter(FS);
            if (file_type == 1)
            {
                Console.Out.WriteLine("  Tag your jpegs with 'random' to give them a random number.");
            }
            Console.Out.WriteLine("  What tag will you add to the file name?");
            String tag = reader.readString();
            //Console.Out.WriteLine(("Exporting to:" + exportFolder));
            if (file_type == 1)
            {
                exporter.writeJPEGFiles(exp_1, exp_5, exp_25, exp_100, exp_250, tag);
                // stringToFile( json, "test.txt");
            }
            else if(file_type == 2)
            {
                exporter.writeJSONFile(exp_1, exp_5, exp_25, exp_100, exp_250, tag);
            }
            else if (file_type == 3)
            {
                exporter.writeQRCodeFiles(exp_1, exp_5, exp_25, exp_100, exp_250, tag);
                // stringToFile( json, "test.txt");
            }
            else if (file_type == 4)
            {
                exporter.writeBarCode417CodeFiles(exp_1, exp_5, exp_25, exp_100, exp_250, tag);
                // stringToFile( json, "test.txt");
            }


            // end if type jpge or stack
            Console.Out.WriteLine("  Exporting CloudCoins Completed.");
        }// end export One
        /* STATIC METHODS */
        
        private static void Fix()
        {
            fixer.continueExecution = true;
            fixer.IsFixing = true;
            fixer.FixAll();
            fixer.IsFixing = false;
        }

        public static void ShowFolders()
        {
            Console.Out.WriteLine(" Root:        " + rootFolder);
            Console.Out.WriteLine(" Import:      " + FS.ImportFolder);
            Console.Out.WriteLine(" Imported:    " + FS.ImportedFolder);
            Console.Out.WriteLine(" Suspect:     " + FS.SuspectFolder);
            Console.Out.WriteLine(" Trash:       " + FS.TrashFolder);
            Console.Out.WriteLine(" Bank:        " + FS.BankFolder);
            Console.Out.WriteLine(" Fracked:     " + FS.FrackedFolder);
            Console.Out.WriteLine(" Templates:   " + FS.TemplateFolder);
            //Console.Out.WriteLine(" Directory:   " + FS.di);
            Console.Out.WriteLine(" Counterfeits:" + FS.CounterfeitFolder);
            Console.Out.WriteLine(" Export:      " + FS.ExportFolder);
            Console.Out.WriteLine(" Lost:        " + FS.LostFolder);

            //QRCodeGenerator qrGenerator = new QRCodeGenerator();
            //QRCodeData qrCodeData = qrGenerator.CreateQrCode("The text which should be encoded.", QRCodeGenerator.ECCLevel.Q);
            //QRCode qrCode = new QRCode(qrCodeData);
            //Bitmap qrCodeImage = qrCode.GetGraphic(20);

            //qrCodeImage.Save("qrcode.jpeg");


        } // end show folders

        public static void Setup()
        {
            // Create the Folder Structure
            FS.CreateFolderStructure();
            // Populate RAIDA Nodes
            raida = RAIDA.GetInstance();
            //raida.Echo();
            FS.LoadFileSystem();

            //Connect to Trusted Trade Socket
            tts = new TrustedTradeSocket("wss://escrow.cloudcoin.digital/ws/", 10, OnWord, OnStatusChange, OnReceive, OnProgress);
            tts.Connect().Wait();
            //Load Local Coins

            //  Console.Read();
        }

        static async Task SendCoinsTT()
        {
            Console.Out.WriteLine("What is the recipients secred word?");
            string word = reader.readString();
            Console.Out.WriteLine("How Many CloudCoins are you Sending?");
            int amount = reader.readInt();
            int total = 0;
            Banker bank = new Banker(FS);
            int[] bankTotals = bank.countCoins(FS.BankFolder);
            int[] frackedTotals = bank.countCoins(FS.FrackedFolder);
            int exp_1 = 0;
            int exp_5 = 0;
            int exp_25 = 0;
            int exp_100 = 0;
            int exp_250 = 0;
            if (amount >= 250 && bankTotals[5] + frackedTotals[5] > 0)
            {
                exp_250 = ((amount / 250) < (bankTotals[5] + frackedTotals[5])) ? (amount / 250) : (bankTotals[5] + frackedTotals[5]);
                amount -= (exp_250 * 250);
                total += (exp_250 * 250);
            }
            if (amount >= 100 && bankTotals[4] + frackedTotals[4] > 0)
            {
                exp_100 = ((amount / 100) < (bankTotals[4] + frackedTotals[4])) ? (amount / 100) : (bankTotals[4] + frackedTotals[4]);
                amount -= (exp_100 * 100);
                total += (exp_100 * 100);
            }
            if (amount >= 25 && bankTotals[3] + frackedTotals[3] > 0)
            {
                exp_25 = ((amount / 25) < (bankTotals[3] + frackedTotals[3])) ? (amount / 25) : (bankTotals[3] + frackedTotals[3]);
                amount -= (exp_25 * 25);
                total += (exp_25 * 25);
            }
            if (amount >= 5 && bankTotals[2] + frackedTotals[2] > 0)
            {
                exp_5 = ((amount / 5) < (bankTotals[2] + frackedTotals[2])) ? (amount / 5) : (bankTotals[2] + frackedTotals[2]);
                amount -= (exp_5 * 5);
                total += (exp_5 * 5);
            }
            if (amount >= 1 && bankTotals[1] + frackedTotals[1] > 0)
            {
                exp_1 = (amount < (bankTotals[1] + frackedTotals[1])) ? amount : (bankTotals[1] + frackedTotals[1]);
                amount -= (exp_1);
                total += (exp_1);
            }
            Exporter exporter = new Exporter(FS);
            exporter.writeJSONFile(exp_1, exp_5, exp_25, exp_100, exp_250, "TrustedTrade");
            string path = FS.ExportFolder + Path.DirectorySeparatorChar + total + ".CloudCoins.TrustedTrade.stack";
            Console.Out.WriteLine("Sending " + path);
            string stack = File.ReadAllText(path);
            await tts.SendCoins(word, stack);
        }

        #region TrustedTradeCallbacks
        static bool OnWord(string word)
        {
            tts.secretWord = word;
            Console.WriteLine("Received Secret Word: " + word);
            return true;
        }

        static bool OnStatusChange()
        {
            Console.WriteLine("Status Changed: " + tts.GetStatus());
            if (tts.GetStatus() == "Coins sent")
            {
                var filenames = new DirectoryInfo(FS.ExportFolder).GetFiles("*.CloudCoins.TrustedTrade.stack");
                foreach (var i in filenames)
                {
                    File.Delete(i.FullName);
                }
                DisplayMenu();
            }
            else if (tts.GetStatus() == "Error")
            {
                Console.WriteLine(tts.GetError());
            }
            return true;
        }

        static bool OnProgress(string i)
        {
            Console.WriteLine("Progress" + i + "%");
            return true;
        }

        static bool OnReceive(string hash)
        {
            Console.WriteLine("https://escrow.cloudcoin.digital/cc.php?h=" + hash);
            DownloadCoin(hash);
            return true;
        }

        static async void DownloadCoin(string hash)
        {
            using (System.Net.Http.HttpClient cli = new System.Net.Http.HttpClient())
            {
                var httpResponse = await cli.GetAsync("https://escrow.cloudcoin.digital/cc.php?h=" + hash);
                var ccstack = await httpResponse.Content.ReadAsStringAsync();
                File.WriteAllText(FS.ImportFolder + Path.DirectorySeparatorChar + "CloudCoins.FromTrustedTrade.stack", ccstack);
                await RAIDA.ProcessCoins(NetworkNumber);
                DisplayMenu();
            }
        }
        #endregion
    }
}
