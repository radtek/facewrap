﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Face;
using Emgu.CV.Structure;
using FaceDetection;

using AForge.Video.DirectShow;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Drawing.Imaging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;
using System.Configuration;
using System.Net.Http;
using System.Threading;
using System.Reflection;

namespace face
{

    public partial class FormFace : Form
    {
        private delegate void UpdateStatusDelegate(string status);
        string FileNameId = string.Empty;
        string[] FileNameCapture = new string[] { string.Empty, string.Empty, string.Empty };
        string dllpath = @"idr210sdk";
        ComparedInfo upload = new ComparedInfo { address = "temp", operatingagency = "hehe" };
        int continuouscapture = 0;
        string version = string.Empty;
        //[DllImport(@"idr210sdk\sdtapi.dll")]
        //public extern static int Routon_IC_HL_ReadCardSN(StringBuilder SN);
        //[DllImport(@"idr210sdk\sdtapi.dll")]
        // extern static int Routon_IC_HL_ReadCard(int SID, int BID, int KeyType, byte[] Key, byte[] data);

        [DllImport(@"idr210sdk\sdtapi.dll")]
        public extern static int InitComm(int iPort);
        [DllImport(@"idr210sdk\sdtapi.dll")]
        public extern static int CloseComm();
        [DllImport(@"idr210sdk\sdtapi.dll")]
        public extern static int Authenticate();

        [DllImport(@"idr210sdk\sdtapi.dll")]
        public extern static int ReadBaseMsg(byte[] pMsg, int len);

        //[DllImport(@"idr210sdk\sdtapi.dll")]
        //public extern static int ReadNewAppMsg(byte[] pMsg, out int len);
        private Thread _tCheckSelfUpdate;
        private FilterInfoCollection videoDevices;
        private string sourceImage = string.Empty;
        private string currentImage = string.Empty;
        //   FisherFaceRecognizer recognizer = new FisherFaceRecognizer(10, 10.0);
        List<Image<Gray, byte>> trainingImages = new List<Image<Gray, byte>>();//Images
        List<string> Names_List = new List<string>(); //labels
        List<int> Names_List_ID = new List<int>();
        private string playpath = string.Empty;
        private string directory = string.Empty;
        //   VideoCapture grabber;
        //   Image<Bgr, Byte> currentFrame; //current image aquired from webcam for display
        //  Image<Gray, byte> result, TrainedFace = null; //used to store the result image and trained face
        //   Image<Gray, byte> gray_frame = null; //grayscale current image aquired from webcam for processing
        //   private int facenum = 0;
        string homepath = string.Empty;
        string host = string.Empty;
        string action = string.Empty;
        string idphotofile = string.Empty;
        string capturephotofile = string.Empty;
        bool capturing = true;
        private VideoCapture _capture = null;
        private bool _captureInProgress;
        private Mat _frame;
        public FormFace()
        {
            InitializeComponent();
            homepath = Environment.CurrentDirectory;
            host = ConfigurationManager.AppSettings["host"];
            action = ConfigurationManager.AppSettings["action"];
            CvInvoke.UseOpenCL = false;
            try
            {
                _capture = new VideoCapture();
                _capture.ImageGrabbed += ProcessFrame;
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
            _frame = new Mat();
        }

        private void ProcessFrame(object sender, EventArgs arg)
        {
            if (_capture != null && _capture.Ptr != IntPtr.Zero)
            {
                _capture.Retrieve(_frame, 0);
                // picturecapture1.BackgroundImage = null;
                pictureBoxsource.BackgroundImage = _frame.Bitmap;
                try
                {
                    BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("请让客户摆正头部位置，自动抓拍并检测照片质量。。。") });
                    if (detectfacethread(_frame))
                    //   if (HaveFace(currentFrame))
                    {
                        FileNameCapture[continuouscapture] = Path.GetTempFileName() + "haveface.jpg";
                        _frame.Save(FileNameCapture[continuouscapture]);
                        switch (continuouscapture)
                        {
                            case 0:
                                pictureBoxcurrentimage.BackgroundImage = _frame.Bitmap;
                                break;
                            case 1:
                                picturecapture1.BackgroundImage = _frame.Bitmap;
                                break;
                            default:
                                picturecapture2.BackgroundImage = _frame.Bitmap;
                                break;
                        }
                        BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("照片抓取成功,{0}", continuouscapture) });
                        // UpdateStatus(string.Format("照片抓取成功,{0}", continuouscapture));
                        continuouscapture++;
                        if (continuouscapture > 2)
                        {
                            capturing = false;
                            _capture.Pause();
                            BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("3张照片抓取完成") });
                            //UpdateStatus(string.Format("3张照片抓取完成"));
                        }
                    }
                    GC.Collect(111,GCCollectionMode.Forced);

                }
                catch (Exception ex)
                {
                    BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("in ProcessFrame,{0}", ex.Message) });
                }
            }
        }



        private void buttoncompare_Click(object sender, EventArgs e)
        {
            try
            {
                labelscore.Text = string.Empty;
                if (FileNameId == string.Empty)
                {
                    UpdateStatus(string.Format("请先读取身份证信息！"));
                    return;
                }
                if (continuouscapture < 3)
                {
                    UpdateStatus(string.Format("请先等待人脸照片抓取成功！-{0}", continuouscapture));
                    return;
                }
                labeltip.Text = "正在比对中。。。";
                for (int i = 0; i < 3; i++)
                {
                    if (compareone(FileNameCapture[i], i + 1))
                    {
                        break;
                    }
                }
                labeltip.Text = "比对完成 ！";
                continuouscapture = 0;
                pictureBoxcurrentimage.BackgroundImage = null;

                picturecapture1.BackgroundImage = null;

                picturecapture2.BackgroundImage = null;
                pictureid.BackgroundImage = null;
                GC.Collect(111, GCCollectionMode.Forced);
            }
            catch (Exception ex)
            {
                UpdateStatus(string.Format("exception :{0},{1},{2}", FileNameCapture, FileNameId, ex.Message));
            }
        }
        private bool detectfacethread(Mat frame)
        {
            try
            {
                //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("in detectfaceex,{0}", 111) });

                long detectionTime;
                List<Rectangle> faces = new List<Rectangle>();
                List<Rectangle> eyes = new List<Rectangle>();
                DetectFace.Detect(
                  frame, "haarcascade_frontalface_default.xml", "haarcascade_eye.xml",
                  faces, eyes,
                  out detectionTime);
                if (faces.Count == 1 && eyes.Count == 2) return true;

                return false;
            }
            catch (Exception ex)
            {
                BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("in detectfacethread,{0}", ex.Message) });
            }
            return false;
        }
        private bool detectfaceex(Mat frame)
        {
            try
            {
                //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("in detectfaceex,{0}", 111) });

                var capturefile = Path.GetTempFileName() + ".jpg";
                frame.Save(capturefile);

                var a = new System.Diagnostics.Process();
                a.StartInfo.UseShellExecute = false;
                a.StartInfo.RedirectStandardOutput = true;
                a.StartInfo.CreateNoWindow = true;
                a.StartInfo.Arguments = capturefile;
                a.StartInfo.FileName = "detectFace.exe";
                a.Start();
                a.WaitForExit();
                var ret = a.ExitCode;
                if (ret == 1234)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("in detectfaceex,{0}", ex.Message) });
            }
            return false;
        }
        bool compareone(string capturefile, int index)
        {
            //var stop = new Stopwatch();
            //stop.Start();
            var a = new System.Diagnostics.Process();
            a.StartInfo.UseShellExecute = false;
            a.StartInfo.RedirectStandardOutput = true;
            a.StartInfo.WorkingDirectory = Path.Combine(homepath, "compare");
            a.StartInfo.CreateNoWindow = true;
            a.StartInfo.Arguments = string.Format(" {0} {1}", capturefile, FileNameId);
            //   UpdateStatus(string.Format("files:{0}", a.StartInfo.Arguments));
            capturephotofile = capturefile;
            a.StartInfo.FileName = Path.Combine(homepath, "compare", "FaceCompareCon.exe");
            //  a.StartInfo.FileName = Path.Combine(homepath, "compare", "ccompare.exe");
            a.Start();
            a.WaitForExit();
            var ret = a.ExitCode;
            //  stop.Stop();
            //  UpdateStatus(string.Format("time elapsed:{0},exitcode={1}", stop.ElapsedMilliseconds, ret));
            //if (ret == 1)
            //{
            //    MessageBox.Show("比对成功，是同一个人");
            //               FileNameId = string.Empty;
            //    return true;
            //}
            //else return false;
            var resultfile = Path.Combine(homepath, "compare", "compareresult.txt");
            var result = JsonConvert.DeserializeObject<result>(File.ReadAllText(resultfile));
            UpdateStatus(string.Format("result score:{0}", result.score));
            labelscore.Text = ((int)(result.score)).ToString() + "%";
            if (result.ok)
            {
                switch (result.status)
                {
                    case CompareStatus.unkown:
                        break;
                    case CompareStatus.success:
                        UpdateStatus(string.Format("第{1}张照片对比是同一个人，WARNING_VALUE={0}", 73, index));
                        var th = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(uploadinfo));
                        th.Start(upload);
                        File.Delete(resultfile);
                        labeltip.Text = "比对完成 ！";
                        MessageBox.Show("比对成功，是同一个人");
                        FileNameId = string.Empty;
                        break;
                    case CompareStatus.failure:
                        UpdateStatus(string.Format("第{1}张照片对比不是同一个人，WARNING_VALUE={0}", 73, index));
                        break;
                    case CompareStatus.uncertainty:
                        UpdateStatus(string.Format("第{1}张照片不确定是否同一人，WARNING_VALUE={0}", 73, index));
                        break;
                    default:
                        break;
                }
            }
            else
            {
                UpdateStatus(string.Format("第{1}张照片比对出错，{0}", result.errcode, index));
            }

            if (result.ok && result.status == CompareStatus.success) return true;
            else return false;
        }
        private void CheckUpdate()
        {

            var lv = long.Parse(version.Replace(".", ""));
            var url = string.Format("http://{0}/GetFaceDesktopUpdatePackage?version={1}", host, lv);
            var srcString = string.Empty;
            var update = true;
            do
            {
                try
                {
                    //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", 111) });
                    using (var handler = new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip })
                    using (var http = new HttpClient(handler))
                    {
                        //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", 222) });
                        var response = http.GetAsync(url);
                        //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", "234234") });
                        if (!response.Result.IsSuccessStatusCode)
                        {
                            //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("no update") });
                            Thread.Sleep(1000 * 60 * 60);
                            continue;
                        }
                        //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", 444) });
                        srcString = response.Result.Content.ReadAsStringAsync().Result;
                        if (response.Result.StatusCode == HttpStatusCode.OK)
                        {
                            var path = Path.GetTempFileName() + ".exe";// Path.Combine(exportPath, ui.Name);
                            BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", 2222) });
                            File.WriteAllBytes(path, response.Result.Content.ReadAsByteArrayAsync().Result);
                            BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("软件更新下载 {0}，{1} ok :", version, lv) });
                            if (MessageBox.Show("软件有新的版本，点击确定开始升级。", "确认", MessageBoxButtons.OKCancel,
                                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.OK)
                            {
                                Process.Start(path);
                                Process.GetCurrentProcess().Kill();
                            }
                        }

                        // var res = response.Result;
                        // string ss = res.Content.ReadAsStringAsync().Result;
                        //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", srcString) });
                        //   BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("软件更新下载--{0},", res.StatusCode) });
                        // BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", response.Status) });
                    }
                    //try
                    //{
                    //    //var ui = JsonConvert.DeserializeObject<UpdateInfo>(srcString);
                    //    //if (ui.Name == string.Empty)
                    //    BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", 1111) });
                    //    //   var exportPath = AppDomain.CurrentDomain.BaseDirectory;
                    //    if (string.IsNullOrEmpty(srcString))
                    //    {
                    //        Thread.Sleep(1000 * 60 * 60);
                    //        continue;
                    //    }
                    //    var path = Path.GetTempFileName() + ".exe";// Path.Combine(exportPath, ui.Name);
                    //    BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", 2222) });
                    //    File.WriteAllBytes(path, Convert.FromBase64String(srcString.Substring(1,srcString.Length-2)));
                    //    BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", 3333) });
                    //    BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("软件更新下载 {0}，{1} ok :",   version,lv) });
                    //    if (MessageBox.Show("软件有新的版本，点击确定开始升级。", "确认", MessageBoxButtons.OKCancel,
                    //        MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.OK)
                    //    {
                    //        Process.Start(path);
                    //        Process.GetCurrentProcess().Kill();
                    //    }
                    //}
                    //catch (Exception ex)
                    //{
                    //    BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("CheckUpdate processing :{0},url={1},{2}", version, url, ex.Message) });
                    //}
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("发送请求时出错"))
                        BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("软件更新查询:{0},url={1},{2}", version, url, "网站可能在更新，下次启动再查。") });
                    else
                        BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("软件更新查询 error:{0},url={1},{2}", version, url, ex.Message) });
                }
                Thread.Sleep(1000 * 60 * 60);
            } while (update);
        }
        private void uploadinfo(object obj)
        {
            var ui = (ComparedInfo)obj;
            ui.capturephoto = File.ReadAllBytes(capturephotofile);
            ui.idphoto = File.ReadAllBytes(idphotofile);

            //  var url = string.Format("http://{0}/{1}", "localhost:5000", "PostCompared");
            var url = string.Format("http://{0}/{1}", host, action);// "api/Trails");// 
            try
            {
                //   BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", url) });

                var handler = new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip };
                //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", 222) });
                using (var http = new HttpClient(handler))
                {
                    var content = new StringContent(JsonConvert.SerializeObject(ui));
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    var hrm = http.PostAsync(url, content);
                    var response = hrm.Result;
                    // BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("capturefile.{0},{1}", capturephotofile, ui.capturephoto) });
                    string srcString = response.Content.ReadAsStringAsync().Result;
                    //   BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", srcString) });
                    BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", response.StatusCode) });
                    //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", hrm.Status) });
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", ex.Message) });
            }
        }


        private void UpdateStatus(string status)
        {
            richTextBox1.AppendText(Environment.NewLine + string.Format("{0}--{1}", DateTime.Now, status));
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            richTextBox1.ScrollToCaret();
        }
        private void ReleaseData()
        {
            if (_capture != null)
                _capture.Dispose();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            textBoxname.Visible = false;
            textBoxid.Visible = false;
            buttongetresult.Visible = false;
            buttoncloudcompare.Visible = false;
            try
            {
                version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                labelversion.Text = " 版本 ： " + version;
                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                if (videoDevices.Count == 0)
                {
                    UpdateStatus("没有摄像头，无法开始拍照，请连接摄像头！");
                    return;
                }
                //grabber = new VideoCapture();
                //grabber.QueryFrame();

                //Application.Idle += new EventHandler(FrameGrabber);
                _capture.Start();
                _tCheckSelfUpdate = new Thread(new ThreadStart(CheckUpdate));
                _tCheckSelfUpdate.Start();
            }
            catch (ApplicationException aex)
            {
                UpdateStatus("No local capture devices: " + aex.Message);
            }
            catch (Exception ex)
            {
                UpdateStatus("No local capture devices--" + ex.Message);
            }
        }

        //void FrameGrabber(object sender, EventArgs e)
        //{
        //    try
        //    {
        //        using (currentFrame = grabber.QueryFrame().ToImage<Bgr, Byte>())
        //        {
        //            if (currentFrame != null)
        //            {
        //                pictureBoxsource.BackgroundImage = currentFrame.Bitmap;

        //                if (HaveFace(currentFrame))
        //                {
        //                    FileNameCapture[continuouscapture] = Path.GetTempFileName() + "haveface.jpg";
        //                    currentFrame.Save(FileNameCapture[continuouscapture]);
        //                    switch (continuouscapture)
        //                    {
        //                        case 0:
        //                            pictureBoxcurrentimage.BackgroundImage = currentFrame.Bitmap;
        //                            break;
        //                        case 1:
        //                            picturecapture1.BackgroundImage = currentFrame.Bitmap;
        //                            break;
        //                        default:
        //                            picturecapture2.BackgroundImage = currentFrame.Bitmap;
        //                            break;
        //                    }

        //                    UpdateStatus(string.Format("照片抓取成功,{0}", continuouscapture));
        //                    continuouscapture++;
        //                    if (continuouscapture > 2)
        //                    {
        //                        capturing = false;
        //                        //Application.Idle -= new EventHandler(FrameGrabber);
        //                        //grabber.Dispose();

        //                        UpdateStatus(string.Format("3张照片抓取完成"));
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        UpdateStatus(string.Format("in FrameGrabber,{0}", ex.Message));
        //    }
        //}
        bool HaveFace(Image<Bgr, Byte> fname)
        {
            long detectionTime;
            List<Rectangle> faces = new List<Rectangle>();
            List<Rectangle> eyes = new List<Rectangle>();
            DetectFace.Detect(
              fname, "haarcascade_frontalface_default.xml", "haarcascade_eye.xml",
              faces, eyes,
              out detectionTime);
            if (faces.Count == 1 && eyes.Count == 2) return true;

            return false;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                //Application.Idle -= new EventHandler(FrameGrabber);
                //grabber.Dispose();
                //  recognizer.Dispose();
                _tCheckSelfUpdate.Abort();
            }
            catch (Exception)
            {
            }
        }

        private void buttonstopcapture_Click(object sender, EventArgs e)
        {
            //   UpdateStatus(string.Format("stop click"));
            try
            {
                if (_capture != null)
                {
                    _capture.Pause();
                }
                capturing = false;
                //Application.Idle -= new EventHandler(FrameGrabber);
                //grabber.Dispose();
            }
            catch (Exception ex)
            {
                UpdateStatus(string.Format("stopcapture:{0}", ex));
            }
        }
        private void buttonrestart_Click(object sender, EventArgs e)
        {

            try
            {
                if (_capture != null)
                {
                    _capture.Start();
                }
                if (capturing) return;
                //  UpdateStatus(string.Format("restart click"));
                continuouscapture = 0;

                pictureBoxcurrentimage.BackgroundImage = null;

                picturecapture1.BackgroundImage = null;

                picturecapture2.BackgroundImage = null;



                //grabber = new VideoCapture();
                //grabber.QueryFrame();
                //Application.Idle += new EventHandler(FrameGrabber);
                capturing = true;

            }
            catch (Exception ex)
            {
                UpdateStatus(string.Format("restart:{0}", ex));
            }
        }
        private void buttonreadid_Click(object sender, EventArgs e)
        {
            int ret;
            int iPort = 1;
            try
            {
                // var nm=new NativeMethods();
                ret = InitComm(iPort);
                if (ret != 0)
                {
                    var ok = true;
                    do
                    {
                        ret = Authenticate();
                        if (ret != 0)
                        {
                            ok = false;

                            var Msg = new byte[200];
                            ret = ReadBaseMsg(Msg, 0);
                            if (ret > 0)
                            {
                                upload.name = System.Text.Encoding.Default.GetString(Msg.Take(31).ToArray());
                                UpdateStatus(string.Format(upload.name));
                                upload.gender = System.Text.Encoding.Default.GetString(Msg.Skip(31).Take(3).ToArray());
                                UpdateStatus(string.Format(upload.gender));
                                upload.nation = System.Text.Encoding.Default.GetString(Msg.Skip(34).Take(10).ToArray());
                                UpdateStatus(string.Format(upload.nation));
                                upload.birthday = System.Text.Encoding.Default.GetString(Msg.Skip(44).Take(9).ToArray());
                                UpdateStatus(string.Format(upload.birthday));
                                upload.idaddress = Encoding.Default.GetString(Msg.Skip(53).Take(71).ToArray());
                                UpdateStatus(string.Format(upload.idaddress));
                                upload.id = System.Text.Encoding.Default.GetString(Msg.Skip(124).Take(19).ToArray());
                                UpdateStatus(string.Format(upload.id));
                                upload.issuer = System.Text.Encoding.Default.GetString(Msg.Skip(143).Take(31).ToArray());
                                UpdateStatus(string.Format(upload.issuer));
                                upload.startdate = System.Text.Encoding.Default.GetString(Msg.Skip(174).Take(9).ToArray());
                                UpdateStatus(string.Format(upload.startdate));
                                upload.enddate = Encoding.Default.GetString(Msg.Skip(183).Take(9).ToArray());
                                UpdateStatus(string.Format(upload.enddate));
                                var FileNameIdtmp = Path.Combine(homepath, dllpath, "photo.bmp");
                                FileNameId = Path.GetTempFileName();
                                idphotofile = FileNameId;
                                using (var jpg = new Bitmap(FileNameIdtmp))
                                {
                                    jpg.Save(FileNameId, ImageFormat.Jpeg);
                                }
                                //  Image.FromFile(FileNameIdtmp).Save(FileNameId, ImageFormat.Jpeg);
                                //using (var img = image.fromfile(filenameid))
                                //{
                                //    pictureid.backgroundimage = img;
                                //}
                                pictureid.BackgroundImage = Image.FromFile(FileNameId);
                                UpdateStatus(string.Format("身份证信息读取成功"));
                            }
                        }
                        else
                        {
                            UpdateStatus(string.Format("请将身份证放在读卡器上 !"));
                            System.Threading.Thread.Sleep(500);
                            continue;
                        }
                    } while (ok);
                }

                ret = CloseComm();
            }
            catch (Exception ex)
            {
                UpdateStatus(string.Format("身份证读卡器操作--{0} !", ex.Message));
            }
        }



        private void button1_Click(object sender, EventArgs e)//无证件上传云端比对
        {
            if (textBoxid.Text.Length < 15)
            {
                UpdateStatus(string.Format("请输入正确的身份证号！"));
                return;
            }
            if (continuouscapture < 3)
            {
                UpdateStatus(string.Format("请先等待人脸照片抓取成功！-{0}", continuouscapture));
                return;
            }
            var ui = new NoidInput();
            for (int i = 0; i < 3; i++)
            {
                switch (i)
                {
                    case 0:
                        ui.pic1 = File.ReadAllBytes(FileNameCapture[i]);
                        break;
                    case 1:
                        ui.pic2 = File.ReadAllBytes(FileNameCapture[i]);
                        break;
                    default:
                        ui.pic3 = File.ReadAllBytes(FileNameCapture[i]);
                        break;
                }
            }
            ui.id = textBoxid.Text;
            ui.name = textBoxname.Text;

            var url = string.Format("http://{0}/{1}", host, "NoidUpload");
            try
            {
                //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", url) });

                var handler = new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip };
                //      BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", 222) });
                using (var http = new HttpClient(handler))
                {
                    //    BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", 333) });
                    var content = new StringContent(JsonConvert.SerializeObject(ui));
                    //   BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("upload.{0},", 444) });
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    var hrm = http.PostAsync(url, content);
                    var response = hrm.Result;
                    string srcString = response.Content.ReadAsStringAsync().Result;
                    //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("NoidUpload.{0},", srcString) });
                    BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("NoidUpload.{0},", response.StatusCode) });
                    //   BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("NoidUpload.{0},", hrm.Status) });
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        continuouscapture = 0;
                        pictureBoxcurrentimage.BackgroundImage = null;

                        picturecapture1.BackgroundImage = null;

                        picturecapture2.BackgroundImage = null;
                    }
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("NoidUpload.{0},", ex.Message) });
            }
        }
        private void buttongetresult_Click(object sender, EventArgs e)
        {
            var url = string.Format("http://{0}/{1}?businessnumber={2}", host, "GetNoidResult", "demo");
            try
            {
                //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("GetNoidResult--11.{0},", url) });
                var handler = new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip };
                using (var http = new HttpClient(handler))
                {
                    //var content = new StringContent(JsonConvert.SerializeObject(new NoidResultInput { businessnumber="demo"}));
                    //content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    var hrm = http.GetAsync(url);
                    var response = hrm.Result;
                    string srcString = response.Content.ReadAsStringAsync().Result;
                    //  BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("GetNoidResult.{0},", srcString) });
                    BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("GetNoidResult.{0},", response.StatusCode) });
                    //   BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("GetNoidResult.{0},", hrm.Status) });
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        //continuouscapture = 0; 0-no result,1-success,2-failure,3-uncertainty
                        var a = JsonConvert.DeserializeObject<List<NoidResult>>(srcString);
                        foreach (var b in a)
                        {
                            BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("id.{0},{1}", b.id, b.status) });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { string.Format("GetNoidResult.{0},", ex.Message) });
            }
        }

        private void buttonnoid_Click(object sender, EventArgs e)
        {
            try
            {
                labeltip.Text = string.Empty;
                labelscore.Text = string.Empty;
                textBoxname.Visible = true;
                textBoxid.Visible = true;
                buttongetresult.Visible = true;
                buttoncloudcompare.Visible = true;


                pictureid.Visible = false;
                buttoncompare.Visible = false;
                buttonreadid.Visible = false;
                BackgroundImage = Image.FromFile("noid.jpg");


            }
            catch (Exception ex)
            {
                UpdateStatus(string.Format("buttonnoid_Click:{0}", ex.Message));
            }
        }

        private void buttonhaveid_Click(object sender, EventArgs e)
        {
            try
            {
                labeltip.Text = string.Empty;
                labelscore.Text = string.Empty;
                pictureid.Visible = true;
                buttoncompare.Visible = true;
                buttonreadid.Visible = true;


                textBoxname.Visible = false;
                textBoxid.Visible = false;
                buttongetresult.Visible = false;
                buttoncloudcompare.Visible = false;
                BackgroundImage = Image.FromFile("haveid.jpg");
            }
            catch (Exception ex)
            {
                UpdateStatus(string.Format("haveid:{0}", ex.Message));
            }
        }

        private void buttonclose_Click(object sender, EventArgs e)
        {
            try
            {
                _tCheckSelfUpdate.Abort();
            }
            catch (Exception) { }
            Close();
        }

        private void buttonmin_Click(object sender, EventArgs e)
        {
            // this.Visible = false;
            //  ShowInTaskbar = true;
            //   Hide();
            WindowState = FormWindowState.Minimized;
        }
    }
}
