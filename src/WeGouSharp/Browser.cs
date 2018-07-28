using log4net;
using System;
using System.IO;
using System.Text;
using static WeGouSharp.Tools;
using System.Linq;
using System.Runtime.InteropServices;
using WeGouSharp.YunDaMa;
using System.Threading.Tasks;
using OpenQA.Selenium.Firefox;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using WeGouSharp.Infrastructure;

namespace WeGouSharp
{
    public class Browser
    {
        private FirefoxDriver _driver;

        private List<string> _tabs = new List<string>();

        IConfiguration _config;

        ILog _logger;

        public Browser(ILog logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            FirefoxProfile ffProfile = new FirefoxProfile();
            ffProfile.SetPreference("browser.download.folderList", 1); //0:desktop 1:download folder 2:custom
            ffProfile.SetPreference("browser.helperApps.neverAsk.saveToDisk", "text/plain");

            FirefoxOptions ffopt = new FirefoxOptions() {Profile = ffProfile};
            _driver = LaunchFireFox(ffopt);

            var homeAddr = "http://weixin.sogou.com/";
            _driver.Navigate().GoToUrl(homeAddr);

            Console.WriteLine(_driver.PageSource);
            _tabs = _driver.WindowHandles.ToList();
        }


        public FirefoxDriver LaunchFireFox(FirefoxOptions option)
        {
            var browserPath = "";
            //folder containe geckodriver            
            var geckodriverPath = "/home/hoyho/workspace/WeGouSharp/src/WeGouSharp/Resource/firefox_linux/";

            FirefoxDriver driver = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && IsRunWithXServer()) //linux with desktop environemnt
            {
                geckodriverPath = "Resource/firefox_linux/";
                browserPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resource/firefox_linux/firefox");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                browserPath = _config["FireFoxPath_OSX"];
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                geckodriverPath = "";
                browserPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Resource/firefox_windows/firefox.exe");
            }

            var fds = FirefoxDriverService.CreateDefaultService(geckodriverPath);
            //fds.FirefoxBinaryPath = browserPath;
            option.AddArgument("-headless");

            driver = new FirefoxDriver(fds, option, TimeSpan.FromMinutes(1));

            return driver;
        }


        /// <summary>
        /// 输入网址返回页面内容,如果有验证码则抛出异常
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        /// <exception cref="WechatSogouVcodeException"></exception>
        public Task<string> GetPageWithoutVcodeAsync(string url)
        {
            return Task.Run(() =>
            {
                _driver.Navigate().GoToUrl(url);
                var pageSrc = _driver.PageSource;
                if (pageSrc.Contains("用户您好，您的访问过于频繁，为确认本次访问为正常用户行为，需要您协助验证"))
                {
                    throw new WechatSogouVcodeException("vcode")
                    {
                        VisittingUrl = _driver.Url
                    };
                }

                return _driver.PageSource;
            });
        }

        /// <summary>
        /// 输入网址返回页面内容,即使包含验证码
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public Task<string> GetPageAsync(string url)
        {
            return Task.Run(() =>
            {
                _driver.Navigate().GoToUrl(url);
                return _driver.PageSource;
            });
        }


        //微信文章页出现的验证码
        public async Task HandleWxVcodeAsync(string vCodeUrl, bool useCloudDecode = true)
        {
            return;
        }


        //搜狗页的验证码
        public async Task<bool> HandleSogouVcodeAsync(string vCodeUrl, bool useCloudDecode = false)
        {
            await GetPageAsync(vCodeUrl);

            var base64Img = _driver.ExecuteScript(@"
    var c = document.createElement('canvas');
    var ctx = c.getContext('2d');
    var img = document.getElementById('seccodeImage');
    c.height=img.height;
    c.width=img.width;
    ctx.drawImage(img, 0, 0,img.width, img.height);
    var base64String = c.toDataURL();
    return base64String;
    ") as string;

            base64Img = base64Img.Replace("data:image/png;base64,", "");


            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("请输入验证码：   ");
                await DisplayImageFromBase64Async(base64Img);
            }
            else
            {
                Console.WriteLine(
                    @"your system is not support showing image in console, please open captcha from ./captcha/vcode");
                SaveImage(base64Img, "vcode.jpg");
                Console.WriteLine("请输入验证码：");
            }

            string verifyCode;
            if (useCloudDecode)
            {
                var decoder = ServiceProviderAccessor.ServiceProvider.GetService(typeof(IDecode)) as IDecode;
                verifyCode = decoder.OnlineDecode("chaptcha/vcode.jpg");
            }
            else
            {
                verifyCode = Console.ReadLine();
            }


            var codeInput = _driver.FindElementById("seccodeInput");
            codeInput.SendKeys(verifyCode);

            _driver.FindElementById("submit").Click();
            return true;
        }


        private bool IsRunWithXServer()
        {
            var cmd = @"
            if xhost >& /dev/null ; 
            then echo 'True'
            else echo 'False' ;
            fi";
            var rs = cmd.RunAsShell().Replace("\n", "");

            if (rs.Trim().ToLower() == "true")
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}