﻿using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using Selenium.Extensions;
using Sl.Selenium.Extensions;
using Sl.Selenium.Extensions.Chrome;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Selenium.WebDriver.UndetectedChromeDriver
{
    public class UndetectedChromeDriver : Sl.Selenium.Extensions.ChromeDriver
    {
        protected UndetectedChromeDriver(ChromeDriverParameters args)
            : base(args)
        {

        }


        private readonly static string[] ProcessNames = { "chrome", "chromedriver", "undetected_chromedriver" };
        public static new void KillAllChromeProcesses()
        {
            foreach (var name in ProcessNames)
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        //ignore errors
                    }
                }
            }

            SlDriver.ClearDrivers(SlDriverBrowserType.Chrome);
        }

        public static new SlDriver Instance(bool Headless = false)
        {
            return Instance("sl_selenium_chrome", Headless);
        }

        public static new SlDriver Instance(String ProfileName, bool Headless = false)
        {
            return Instance(new HashSet<string>(), ProfileName, Headless);
        }

        public static new SlDriver Instance(ISet<string> DriverArguments, String ProfileName, bool Headless = false)
        {
            return Instance(DriverArguments, new HashSet<string>(), ProfileName, Headless);
        }

        public static new SlDriver Instance(ISet<string> DriverArguments, ISet<string> ExcludedArguments, String ProfileName, bool Headless = false)
        {
            var parameters = new ChromeDriverParameters()
            {
                DriverArguments = DriverArguments,
                ExcludedArguments = ExcludedArguments,
                Headless = Headless,
                ProfileName = ProfileName
            };

            return Instance(parameters);
        }


        public static new SlDriver Instance(ChromeDriverParameters args)
        {
            if(args.DriverArguments == null)
                args.DriverArguments = new HashSet<string>();

            if(args.ExcludedArguments == null)
                args.ExcludedArguments = new HashSet<string>();

            if (args.ProfileName == null)
                args.ProfileName = "sl_selenium_chrome";

            if (!_openDrivers.IsOpen(SlDriverBrowserType.Chrome, args.ProfileName))
            {
                UndetectedChromeDriver cDriver = new UndetectedChromeDriver(args);

                _openDrivers.OpenDriver(cDriver);
            }
            return _openDrivers.GetDriver(SlDriverBrowserType.Chrome, args.ProfileName);
        }


        public override void GoTo(string URL)
        {
            var webDriverResult = this.ExecuteScript("return navigator.webdriver");

            if (webDriverResult != null)
            {
                BaseDriver.ExecuteCdpCommand("Page.addScriptToEvaluateOnNewDocument",
                    new Dictionary<string, object>()
                    {
                        {"source", @"
                                Object.defineProperty(window, 'navigator', {
                                       value: new Proxy(navigator, {
                                       has: (target, key) => (key === 'webdriver' ? false : key in target),
                                       get: (target, key) =>
                                           key === 'webdriver'
                                           ? undefined
                                           : typeof target[key] === 'function'
                                           ? target[key].bind(target)
                                           : target[key]
                                       })
                                   });
                        " }
                    });
            }

            // 모바일 User-Agent 및 디바이스 메트릭스 적용 (Chrome 138)
            string mobileUA = "Mozilla/5.0 (Linux; Android 15; SM-S918N) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.7204.63 Mobile Safari/537.36";
            BaseDriver.ExecuteCdpCommand("Network.setUserAgentOverride",
                new Dictionary<string, object>
                {
                    {"userAgent", mobileUA},
                    {"platform", "Android"},
                    {"acceptLanguage", "ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7"},
                    {"userAgentMetadata", new Dictionary<string, object>
                        {
                            {"platform", "Android"},
                            {"mobile", true},
                            {"brands", new[]{ new Dictionary<string,object>{{"brand","Google Chrome"},{"version","138"}} } }
                        }
                    }
                }
            );

            BaseDriver.ExecuteCdpCommand("Emulation.setDeviceMetricsOverride",
                new Dictionary<string, object>
                {
                    {"width", 360},
                    {"height", 800},
                    {"deviceScaleFactor", 3},
                    {"mobile", true}
                }
            );

            // navigator.platform, userAgent, maxTouchPoints, userAgentData 등 모바일로 위장
            BaseDriver.ExecuteCdpCommand("Page.addScriptToEvaluateOnNewDocument",
                new Dictionary<string, object>
                {
                    {"source", @"
                        Object.defineProperty(navigator, 'platform', {get: () => 'Linux armv8l'});
                        Object.defineProperty(navigator, 'userAgent', {get: () => '" + mobileUA + @"'});
                        Object.defineProperty(navigator, 'maxTouchPoints', {get: () => 5});
                        Object.defineProperty(navigator, 'userAgentData', {get: () => ({
                            platform: 'Android',
                            mobile: true,
                            brands: [{brand: 'Google Chrome', version: '138'}]
                        })});
                    "}
                }
            );

            var scriptResult = this.ExecuteScript(@"
               let objectToInspect = window,
                        result = [];
                    while(objectToInspect !== null)
                    { result = result.concat(Object.getOwnPropertyNames(objectToInspect));
                      objectToInspect = Object.getPrototypeOf(objectToInspect); }
                    return result.filter(i => i.match(/.+_.+_(Array|Promise|Symbol)/ig))
            ");

            if (scriptResult != null && ((ReadOnlyCollection<object>)scriptResult).Count > 0)
            {
                BaseDriver.ExecuteCdpCommand("Page.addScriptToEvaluateOnNewDocument",
                    new Dictionary<string, object>()
                    {
                        {"source", @" 
                        let objectToInspect = window,
                        result = [];
                            while(objectToInspect !== null) 
                            { result = result.concat(Object.getOwnPropertyNames(objectToInspect));
                              objectToInspect = Object.getPrototypeOf(objectToInspect); }
                            result.forEach(p => p.match(/.+_.+_(Array|Promise|Symbol)/ig)
                                                &&delete window[p]&&console.log('removed',p))
                    " }
                    }
                );
            }

            base.GoTo(URL);
        }

        public override string DriverName()
        {
            return "undetected_" + base.DriverName();
        }

        public static SlDriver Instance(string ProfileName, ChromeOptions ChromeOptions)
        {
            var driver = (UndetectedChromeDriver)Instance(ProfileName);
            driver.ChromeOptions = ChromeOptions;
            return driver;
        }


        public static SlDriver Instance(string ProfileName, ChromeOptions ChromeOptions, TimeSpan Timeout)
        {
            ChromeDriverParameters cdp = new ChromeDriverParameters() { 
                ProfileName = ProfileName,
                Timeout = Timeout 
            };
            var driver = (UndetectedChromeDriver)Instance(cdp);
            driver.ChromeOptions = ChromeOptions;
            return driver;
        }


        public ChromeOptions ChromeOptions { get; private set; }

        protected override OpenQA.Selenium.Chrome.ChromeDriver CreateBaseDriver()
        {
            var service = OpenQA.Selenium.Chrome.ChromeDriverService.CreateDefaultService(DriversFolderPath(), DriverName());

            service.HostName = "127.0.0.1";

            service.SuppressInitialDiagnosticInformation = true;

            DriverArguments.Add("start-maximized");
            DriverArguments.Add("--disable-blink-features");
            DriverArguments.Add("--disable-blink-features=AutomationControlled");
            DriverArguments.Add("disable-infobars");

            if (this.Headless)
            {
                DriverArguments.Add("headless");
            }
            else
            {
                DriverArguments.Remove("headless");
            }

            DriverArguments.Add("--no-default-browser-check");
            DriverArguments.Add("--no-first-run");


            HashSet<string> argumentKeys = new HashSet<string>(DriverArguments.Select(f => f.Split('=')[0]));

            if (!argumentKeys.Contains("--log-level"))
            {
                DriverArguments.Add("--log-level=0");
            }

            if(ChromeOptions == null)
            {
                ChromeOptions = new ChromeOptions();
            }
            
            foreach (var arg in DriverArguments)
            {
                ChromeOptions.AddArgument(arg);
            }



            ChromeOptions.AddExcludedArgument("enable-automation");
            ChromeOptions.AddAdditionalChromeOption("useAutomationExtension", false);

            foreach (var excluded in ChromeDriverParameters.ExcludedArguments)
            {
                ChromeOptions.AddExcludedArgument(excluded);
            }

            AddProfileArgumentToBaseDriver(ChromeOptions);

            if (ChromeDriverParameters.Timeout != default)
            {
                return new OpenQA.Selenium.Chrome.ChromeDriver(service, ChromeOptions, ChromeDriverParameters.Timeout);
            }
            else
            {
                return new OpenQA.Selenium.Chrome.ChromeDriver(service, ChromeOptions);
            }
        }

        public static bool ENABLE_PATCHER = true;
        protected override void DownloadLatestDriver()
        {
            base.DownloadLatestDriver();

            #region patcher
            if (ENABLE_PATCHER)
            {
                PatchDriver();  
            }
            #endregion
        }

        private void PatchDriver()
        {
            string newCdc = randomCdc(26);
            using (FileStream stream = new FileStream(this.DriverPath(), FileMode.Open, FileAccess.ReadWrite))
            {
                var buffer = new byte[1];
                var str = new StringBuilder("....");

                var read = 0;
                while (true)
                {
                    read = stream.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                        break;

                    str.Remove(0, 1);
                    str.Append((char)buffer[0]);

                    if (str.ToString() == "cdc_")
                    {
                        stream.Seek(-4, SeekOrigin.Current);
                        var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(newCdc);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
            }
        }

        private static string randomCdc(int size)
        {
            Random random = new Random((int)DateTime.Now.Ticks);

            const string chars = "abcdefghijklmnopqrstuvwxyz";


            char[] buffer = new char[size];
            for (int i = 0; i < size; i++)
            {
                buffer[i] = chars[random.Next(chars.Length)];
            }

            buffer[2] = buffer[0];
            buffer[3] = '_';
            return new string(buffer);
        }
    }
}
