﻿// Copyright (c) Zhongkai Fu. All rights reserved.
// https://github.com/zhongkaifu/Seq2SeqSharp
//
// This file is part of Seq2SeqSharp.
//
// Seq2SeqSharp is licensed under the BSD-3-Clause license found in the LICENSE file in the root directory of this source tree.
//
// Seq2SeqSharp is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the BSD-3-Clause License for more details.

using System.IO;
using System.Text;
using AdvUtils;
using Microsoft.AspNetCore.Mvc;
using Seq2SeqSharp.Utils;
using Seq2SeqWebApps;
using SeqWebApps.Models;

namespace SeqWebApps.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private static Dictionary<string, int> ip2calls = new Dictionary<string, int>();
        private static object locker = new object();

        private static string thumbUpFilePath = Path.Combine(Directory.GetCurrentDirectory(), "thumbUp.txt");

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public void SubmitFeedback(string srcInput, string tgtInput, float topP, float temperature, string clientIP, int feedBackType)
        {
            if (feedBackType == 1)
            {
                //Thumb Up
                Logger.WriteLine($"ThumbUp: topP = '{topP}', temperature = '{temperature}', clientIP = '{clientIP}', Source = '{srcInput}', Target = '{tgtInput}'");
                lock (locker)
                {
                    System.IO.File.AppendAllLines(thumbUpFilePath, new string[] { tgtInput });
                }
            }
            else
            {
                //Thumb Down
                Logger.WriteLine($"ThumbDown: topP = '{topP}', temperature = '{temperature}', clientIP = '{clientIP}', Source = '{srcInput}', Target = '{tgtInput}'");
            }
                    
        }

        [HttpPost]
        public IActionResult GenerateText(string srcInput, string tgtInput, int num, float topP, float temperature, string clientIP)
        {
            try
            {
                if (tgtInput == null)
                {
                    Logger.WriteLine($"New Request: topP = '{topP}', temperature = '{temperature}', clientIP = '{clientIP}', Source = '{srcInput}'");
                    tgtInput = "";
                }

                if (String.IsNullOrEmpty(clientIP))
                {
                    clientIP = "unknown_" + DateTime.Now.Microsecond.ToString();
                }

                lock (locker)
                {
                    if (ip2calls.ContainsKey(clientIP))
                    {
                        Logger.WriteLine($"IP '{clientIP}' has call under processing, so we ignore this call.");

                        TextGenerationModel callIgnored = new TextGenerationModel
                        {
                            Output = "!!! Because of service capcity limitation, each client only have one call at a time, then everyone could have better experience. Please cancel the ongoing call at first, and then retry. !!!",
                            DateTime = DateTime.Now.ToString()
                        };

                        return new JsonResult(callIgnored);
                    }
                    else
                    {
                        ip2calls.Add(clientIP, 1);
                    }
                }

                TextGenerationModel textGeneration = new TextGenerationModel
                {
                    Output = CallBackend(srcInput, tgtInput, num, topP, temperature),
                    DateTime = DateTime.Now.ToString()
                };

                lock (locker)
                {
                    ip2calls.Remove(clientIP);
                }

                return new JsonResult(textGeneration);
            }
            catch (Exception e)
            {
                Logger.WriteLine($"Error: '{e.Message}'");
                Logger.WriteLine($"Call stack: '{e.StackTrace}'");
                lock (locker)
                {
                    ip2calls.Remove(clientIP);
                }

                throw;
            }
        }


        private string CallBackend(string srcInputText, string tgtInputText, int tokenNumToGenerate, float topP, float temperature)
        {
            if (String.IsNullOrEmpty(srcInputText))
            {
                srcInputText = "";
            }

            if (String.IsNullOrEmpty(tgtInputText))
            {
                tgtInputText = "";
            }


            srcInputText = srcInputText.Replace("<br />", "");
            tgtInputText = tgtInputText.Replace("<br />", "");

            string[] srcLines = srcInputText.Split("\n");
            string[] tgtLines = tgtInputText.Split("\n");

            srcInputText = String.Join(" ", srcLines);
            tgtInputText = String.Join(" ", tgtLines);

            string outputText = Seq2SeqInstance.Call(srcInputText, tgtInputText, tokenNumToGenerate, topP, temperature);            
            var outputSents = SplitSents(outputText);
            return String.Join("<br />", outputSents);

        }

        private static string[] Split(string text, char[] seps)
        {
            HashSet<char> setSeps = new HashSet<char>();
            foreach (var sep in seps)
            {
                setSeps.Add(sep);
            }

            List<string> parts = new List<string>();
            StringBuilder sb = new StringBuilder();
            foreach (char ch in text)
            {
                sb.Append(ch);
                if (setSeps.Contains(ch) && sb.Length > 1)
                {
                    parts.Add(sb.ToString().Trim());
                    sb = new StringBuilder();
                }
            }

            if (sb.Length > 0)
            {
                parts.Add(sb.ToString());
            }

            return parts.ToArray();
        }

        private bool OnlyPartsInSent(string sent, string[] parts)
        {
            foreach (string part in parts)
            {
                sent = sent.Replace(part, "");
            }

            return sent.Trim().IsNullOrEmpty();
        }

        private List<string> SplitSents(string currentSent)
        {
            List<string> sents = new List<string>();

            HashSet<char> setClosedPunct = new HashSet<char>();
            setClosedPunct.Add('”');
            setClosedPunct.Add('\"');
            setClosedPunct.Add('】');
            setClosedPunct.Add(')');
        
            string[] parts = Split(currentSent, new char[] { '。', '！', '?', '!', '?' });
            for (int i = 0; i < parts.Length; i++)
            {
                string p = String.Empty;
                bool skipNextLine = false;
                if (i < parts.Length - 1)
                {
                    if (setClosedPunct.Contains(parts[i + 1][0]) || OnlyPartsInSent(parts[i + 1], parts))
                    {
                        p = parts[i] + parts[i + 1];
                        skipNextLine = true;
                    }                   
                    else
                    {
                        p = parts[i];
                    }
                }
                else
                {
                    p = parts[i];
                }

                sents.Add(p);

                if (skipNextLine)
                {
                    i++;
                }
            }

            return sents;
        }

    }
}
