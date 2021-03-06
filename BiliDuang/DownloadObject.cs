﻿using BiliDuang.tools;
using BiliDuang.VideoClass;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BiliDuang
{

    public class DownloadObject
    {
        /**
         * 
         * 正数正常,负数不正常
         *  0 下载未开始 
         *  1 下载已暂停
         *  2 下载被手动结束
         *  5 下载开始
         *  
         * -1 链接获取错误
         * -2 下载文件错误
         * -3 速度获取报错
         * -4 下载完成报错
         * -5 合并视频报错
         * 
         * 66 下载完成
         */
        public int status = 0;
        public string message;
        private bool wcusing = false;
        public bool handpause;

        //基本信息
        public string saveto;
        public string aid;
        public string cid;
        public string name
        {
            get
            {
                return "[" + VideoQuality.Name(quality) + "] " + avname;
            }
            set
            {
                return;
            }
        }
        private string _avname;
        public string avname
        {
            set
            {
                value = value.Replace("\\", "＼");
                value = value.Replace("/", "／");
                value = value.Replace(":", "：");
                value = value.Replace("?", "？");
                value = value.Replace("\"", "＂");
                value = value.Replace("<", "＜");
                value = value.Replace(">", "＞");
                value = value.Replace("|", "｜");
                _avname = value;
            }
            get
            {
                return _avname;
            }
        }
        public int blocknum = 0;
        public WebClient wc = new WebClient();


        public List<DownloadUrl> urls = new List<DownloadUrl>();
        public int quality;
        private bool single = false;
        Stopwatch sw = new Stopwatch();

        public int progress;//进度用于进度条
        public double speed;


        public DownloadObject(string aid, string cid, int quality, string saveto, string name, string avname)
        {
            this.aid = aid;
            this.cid = cid;
            this.quality = quality;
            this.saveto = saveto;
            this.name = name;
            this.avname = avname;
        }

        public void LinkStart()
        {
            if (wcusing) return;
            else wc = new WebClient();
            status = 0;
            if (urls.Count == 0)
            {
                if (!GetDownloadUrls())
                {
                    status = -1;
                    return;
                }
            }

            if (urls.Count == 1) single = true;
            try
            {
                wc.Headers.Add("Cookie", User.cookie);
                wc.DownloadFileCompleted += new AsyncCompletedEventHandler(CompletedHandle);
                wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(ProgressChanged);
                //wc.Accept = "*/*";
                //wc.Referer = "https://bilibili.com/";
                wc.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.13; rv:56.0) Gecko/20100101 Firefox/56.0");
                wc.Headers.Add("Origin", "https://www.bilibili.com");
                wc.Headers.Add("Referer", "https://www.bilibili.com");
                Uri uri = new Uri(urls[blocknum].url);
                status = 5;
                if (!single)
                {
                    Directory.CreateDirectory(saveto + "/" + avname);
                    message = "开始下载";
                    //当前暂不支持断点续传,于是我们便把之前的文件删掉吧
                    if (File.Exists(saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type))
                    {
                        FileInfo fi = new FileInfo(saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);
                        if (fi.Length == urls[blocknum].size)
                        {
                            Completed(true, "文件已经存在且大小正确");
                            return;
                        }
                        File.Delete(saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);

                    }
                    Console.WriteLine("Creating Download url: " + urls[blocknum].url + " to " + saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);
                    sw.Start();
                    wcusing = true;
                    wc.DownloadFileAsync(uri, saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);
                }
                else
                {
                    message = "开始下载";
                    if (File.Exists(saveto + "/" + avname + "." + urls[blocknum].type))
                    {
                        FileInfo fi = new FileInfo(saveto + "/" + avname + "." + urls[blocknum].type);
                        if (fi.Length == urls[blocknum].size)
                        {
                            Completed(true, "文件已经存在且大小正确");
                            return;
                        }
                        File.Delete(saveto + "/" + avname + "." + urls[blocknum].type);
                    }
                    File.Delete(saveto + "/" + avname + " - " + name + "_" + blocknum.ToString() + "." + urls[blocknum].type);
                    Console.WriteLine("Creating Download url: " + urls[blocknum].url + " to " + saveto + "/" + avname + " - " + name + "_" + blocknum.ToString() + "." + urls[blocknum].type);
                    sw.Start();
                    wcusing = true;
                    wc.DownloadFileAsync(uri, saveto + "/" + avname + "." + urls[blocknum].type);
                }
            }

            catch (WebException we)
            {
                status = -2;
                message = "文件分片" + blocknum.ToString() + "下载错误: " + we.Message;
                wcusing = false;
            }
            catch (System.NotSupportedException e)
            {
                status = -2;
                message = e.Message;
                wcusing = false;

            }
            catch (Exception e)
            {
                wcusing = false;
                status = -2;
                message = "分片" + blocknum.ToString() + "下载错误: " + e.Message;
            }
        }

        internal void Resume()
        {
            LinkStart();
        }

        internal void Cancel()
        {
            wcusing = false;
            wc.CancelAsync();
            wc.Dispose();
        }

        private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            try
            {
                // 显示下载速度
                speed = Convert.ToDouble(e.BytesReceived) / sw.Elapsed.TotalSeconds;

                if (urls[blocknum].size == -1)
                {
                    urls[blocknum].size = e.TotalBytesToReceive;
                }

                // 进度条
                progress = (e.ProgressPercentage / urls.Count) + (blocknum * 100 / urls.Count);

                // 下载了多少 还剩余多少
                //labelDownloaded.Text = (Convert.ToDouble(e.BytesReceived) / 1024 / 1024).ToString("0.00") + " Mb's" + "  /  " + (Convert.ToDouble(e.TotalBytesToReceive) / 1024 / 1024).ToString("0.00") + " Mb's";
                //正在下载区块 {0}/{5}: {1}/{2}  {3}% 速度:{4}/s <{6}>

                message = string.Format("正在下载区块{0}/{1}: {2}/{3} {4}% 速度:{5}/s <{6}>", blocknum + 1, urls.Count, byteConvert.GetSize(e.BytesReceived), byteConvert.GetSize(e.TotalBytesToReceive), progress.ToString(), byteConvert.GetSize(speed), "NaN");
            }
            catch (Exception ex)
            {
                status = -3;
                message = "进度获取错误" + ex.Message;
            }

        }

        private void CompletedHandle(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled || e.Error != null)
            {
                Completed(false, e.Error.Message);

            }
            else
            {
                Completed(true, "下载完成");
            }
        }

        private void Completed(bool complete, string msg)
        {
            wcusing = false;
            sw.Reset();
            if (status == 1) return;
            if (complete != true)
            {
                status = -4;
                message = "下载未完成,可能是网络中断,正在重试";
                Console.WriteLine("下载出错," + msg);
                LinkStart();
                return;
            }
            else
            {
                message = "下载完成:" + saveto + avname + " - " + name + "." + urls[blocknum].type;
                if (blocknum != urls.Count - 1)
                {
                    if (single)
                    {
                        FileInfo fi = new FileInfo(saveto + "/" + avname + "." + urls[blocknum].type);
                        Console.WriteLine("Download Complete! Downloaded Size: " + fi.Length.ToString() + " Server Size: " + urls[blocknum].size.ToString());
                        if (urls[blocknum].size != -1 && fi.Length != urls[blocknum].size)
                        {
                            Console.WriteLine("Size Error, Try Download Again");
                            message = "区块" + (blocknum + 1).ToString() + "下载出错,正在重试";
                            LinkStart();
                            return;
                        }
                    }
                    else
                    {
                        FileInfo fi = new FileInfo(saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);
                        Console.WriteLine("Download Complete! Downloaded Size: " + fi.Length.ToString() + " Server Size: " + urls[blocknum].size.ToString());
                        if (urls[blocknum].size != -1 && fi.Length != urls[blocknum].size)
                        {
                            Console.WriteLine("Size Error, Try Download Again");
                            message = "区块" + (blocknum + 1).ToString() + "下载出错,正在重试";
                            LinkStart();
                            return;
                        }
                    }
                    blocknum++;
                    LinkStart();
                    return;
                }
                else if (!single)
                {

                    FileInfo fi = new FileInfo(saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);
                    Console.WriteLine("Download Complete! Downloaded Size: " + fi.Length.ToString() + " Server Size: " + urls[blocknum].size.ToString());
                    if (urls[blocknum].size != -1 && fi.Length != urls[blocknum].size)
                    {
                        Console.WriteLine("Size Error, Try Download Again");
                        message = "区块" + (blocknum + 1).ToString() + "下载出错,正在重试";
                        LinkStart();
                        return;
                    }
                    MergeVideo();
                }
                else
                {
                    status = 66;
                    message = "下载完成!";
                }
            }
        }

        private void MergeVideo()
        {
            //Goodbye FFMPEG
            message = "合并分块到一个视频文件中";
            if (urls[0].type == "mp4")
            {
                string fc = "";
                List<string> filenames = new List<string>();
                for (int i = 0; i < urls.Count; i++)
                {
                    filenames.Add(saveto + "/" + avname + "/" + i + "." + urls[i].type);
                }

                foreach (string file in filenames)
                {
                    fc += string.Format("-add \"{0}\" ", file);
                }
                string argu = string.Format("{0}-new \"{1}\"", fc, (saveto + "/" + avname + "." + urls[0].type));
                Process exep = new Process();
                exep.StartInfo.CreateNoWindow = true;
                exep.StartInfo.Arguments = argu;
                //不使用操作系统使用的shell启动进程
                exep.StartInfo.UseShellExecute = false;
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    exep.StartInfo.FileName = Environment.CurrentDirectory + "/tools/mp4box.exe";
                else
                    exep.StartInfo.FileName = "mp4box";
                exep.Start();
                exep.WaitForExit();//关键，等待外部程序退出后才能往下执行
                if (File.Exists(saveto + "/" + avname + "." + urls[0].type))
                {
                    Directory.Delete(saveto + "/" + avname, true);
                    status = 66;
                    message = "下载完成!";
                }
                else
                {
                    status = -5;
                    message = "视频合并出错,下载缓存暂未删除";
                }

            }
            else if (urls[0].type == "flv")
            {
                List<string> filenames = new List<string>();
                for (int i = 0; i < urls.Count; i++)
                {
                    filenames.Add(saveto + "/" + avname + "/" + i + "." + urls[i].type);
                }
                if (FlvMerger.StartMerge(filenames, (saveto + "/" + avname + "." + urls[0].type)))
                {
                    if (File.Exists(saveto + "/" + avname + "." + urls[0].type))
                    {
                        Directory.Delete(saveto + "/" + avname, true);
                        status = 66;
                        message = "下载完成!";
                    }
                    else
                    {
                        status = -5;
                        message = "视频合并出错,下载缓存暂未删除";
                    }
                }
                else
                {
                    status = -5;
                    message = "视频合并出错,下载缓存暂未删除";
                }

            }

        }

        public void Pause()
        {
            status = 1;
            message = "停止中";
            wcusing = false;
            wc.CancelAsync();
            wc.Dispose();
        }

        private bool GetDownloadUrls()
        {
            if (quality < VideoQuality.Q4K)
            {

                //下载链接api为 https://api.bilibili.com/x/player/playurl?avid=44743619&cid=78328965&qn=32 cid为上面获取到的 avid为输入的av号 qn为视频质量
                //https://www.biliplus.com/BPplayurl.php?otype=json&cid=29892777&module=bangumi&qn=16
                WebClient MyWebClient = new WebClient();
                MyWebClient.Credentials = CredentialCache.DefaultCredentials;//获取或设置用于向Internet资源的请求进行身份验证的网络凭据            
                string callback = "";
                try
                {
                    if (!Settings.outland)
                    {
                        MyWebClient.Headers.Add("Cookie", User.cookie);
                        callback = Encoding.UTF8.GetString(MyWebClient.DownloadData(string.Format("https://api.bilibili.com/x/player/playurl?avid={0}&cid={1}&qn={2}", aid, cid, quality.ToString()))); //如果获取网站页面采用的是UTF-8，则使用这句

                    }
                    else
                    {
                        System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; //加上这一句
                        callback = Encoding.UTF8.GetString(MyWebClient.DownloadData(string.Format("https://www.biliplus.com/BPplayurl.php?otype=json&module=bangumi&avid={0}&cid={1}&qn={2}", aid, cid, quality.ToString()))); //如果获取网站页面采用的是UTF-8，则使用这句
                    }
                }
                catch (WebException e)
                {
                    Dialog.Show("无法下载," + e.Message);
                    return false;
                }
                MyWebClient.Dispose();
                if (!Settings.outland)
                {
                    JSONCallback.Player.Player player = JsonConvert.DeserializeObject<JSONCallback.Player.Player>(callback);
                    if (player.code == -404)
                    {
                        Dialog.Show(string.Format("无法下载 {0}({1}), 该视频需要大会员登录下载,请先登录", player.code, player.message), "获取错误");
                        return false;
                    }
                    else if (player.code != 0)
                    {
                        Dialog.Show(string.Format("无法下载 {0}({1})", player.code, player.message), "获取错误");
                        return false;
                    }
                    if (!player.data.accept_quality.Contains(quality))
                    {
                        Console.WriteLine(string.Format("没有指定的画质 {0} ,最高画质为 {1}, 自动下载最高画质{1}", VideoQuality.Name(quality), VideoQuality.Name(player.data.accept_quality[0])));
                        quality = player.data.accept_quality[0];
                        return GetDownloadUrls();//我太懒了,直接递归吧
                    }
                    foreach (JSONCallback.Player.DurlItem Item in player.data.durl)
                    {
                        DownloadUrl du = new DownloadUrl();
                        du.url = Item.url;
                        du.size = Item.size;
                        urls.Add(du);
                    }
                    return true;
                }
                else
                {
                    if (callback == "")
                    {
                        Dialog.Show(string.Format("使用BiliPlus API出错!"), "获取错误");
                        return false;
                    }
                    JSONCallback.BiliPlus.Player player = JsonConvert.DeserializeObject<JSONCallback.BiliPlus.Player>(callback);
                    if (!player.accept_quality.Contains(quality))
                    {
                        Console.WriteLine(string.Format("没有指定的画质 {0} ,最高画质为 {1}, 自动下载最高画质{1}", VideoQuality.Name(quality), VideoQuality.Name(player.accept_quality[0])));
                        quality = player.accept_quality[0];
                        return GetDownloadUrls();//我太懒了,直接递归吧
                    }
                    foreach (JSONCallback.BiliPlus.DurlItem Item in player.durl)
                    {
                        DownloadUrl du = new DownloadUrl();
                        du.url = Item.url;
                        du.size = Item.size;
                        urls.Add(du);
                    }
                    return true;
                }

            }
            else
            {
                string callback = Other.GetHtml("https://www.bilibili.com/video/av" + aid, true);
                string json = Other.TextGetCenter("window.__playinfo__=", "</script>", callback);
                Console.WriteLine(json);
                try
                {
                    JSONCallback.FourKPlayer.FourKPlayer player = JsonConvert.DeserializeObject<JSONCallback.FourKPlayer.FourKPlayer>(json);
                    if (player.code == -404)
                    {
                        Dialog.Show(string.Format("无法下载 {0}({1}), 该视频需要大会员登录下载,请先登录", player.code, player.message), "获取错误");
                        return false;
                    }
                    else if (player.code != 0)
                    {
                        Dialog.Show(string.Format("无法下载 {0}({1})", player.code, player.message), "获取错误");
                        return false;
                    }
                    if (!player.data.accept_quality.Contains(quality))
                    {
                        Console.WriteLine(string.Format("没有指定的画质 {0} ,最高画质为 {1}, 自动下载最高画质{1}", VideoQuality.Name(quality), VideoQuality.Name(player.data.accept_quality[0])));
                        quality = player.data.accept_quality[0];
                        return GetDownloadUrls();//我太懒了,直接递归吧
                    }
                    foreach (JSONCallback.FourKPlayer.VideoItem Item in player.data.dash.video)
                    {
                        if (Item.id != quality) continue;
                        DownloadUrl du = new DownloadUrl();
                        du.type = Item.mimeType.Replace("video/", "");
                        du.url = Item.baseUrl;
                        du.size = -1;//暂不支持检测大小
                        urls.Add(du);
                        du = new DownloadUrl();
                        du.type = "mp3";
                        du.url = player.data.dash.audio[1].baseUrl;
                        du.size = -1;//暂不支持检测大小
                        urls.Add(du);
                        return true;
                    }
                    return false;
                }
                catch (Exception)
                {
                    Console.WriteLine("4K获取出错,正在尝试降级后重试.");
                    quality = VideoQuality.Q1080P60;
                    return GetDownloadUrls();
                }

            }
        }
    }

    public class DownloadUrl
    {
        public string type = "flv";
        public string url;
        public long size;
    }
}