﻿using FC.Framework;
using DotPay.Common;
using DotPay.Command;
using DotPay.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DotPay.QueryService;
using QConnectSDK.Context;
using QConnectSDK;
using System.IO;
using Gma.QrCodeNet.Encoding;
using Gma.QrCodeNet.Encoding.Windows.Render;
using System.Drawing;
using System.Drawing.Imaging;

namespace DotPay.Web.Controllers
{
    public class ProfileController : BaseController
    {
        #region Views

        #region index
        [Route("~/my/index")]
        public ActionResult Index()
        {
            ViewBag.CNYAccount = IoC.Resolve<IAccountQuery>().GetAccountByUserID(this.CurrentUser.UserID, CurrencyType.CNY);
            var txData = IoC.Resolve<ITransactionQuery>().GetUserTransactions(this.CurrentUser.UserID, 20, 1);
            ViewBag.TxRecords = txData != null ? txData.Data : null;
            return View();
        }
        #endregion

        #region Profile
        [Route("~/my/profile")]
        public ActionResult Profile()
        {
            return View();
        }
        #endregion

        #region tx records
        [Route("~/my/txrecords")]
        public ActionResult TxRecords()
        {
            return View();
        }
        #endregion

        #region 修改登录密码
        [Route("~/my/modifyloginpwd")]
        [AllowAnonymous]
        public ActionResult ModifyLoginPwd()
        {
            return View("ModifyLoginPassword");
        }
        #endregion

        #region 修改支付密码
        [Route("~/my/modifypaypwd")]
        [AllowAnonymous]
        public ActionResult ModifyPayPwd()
        {
            return View("ModifyPaymentPassword");
        }
        #endregion

        #region 修改/设置手机号
        [Route("~/my/mobile")]
        [AllowAnonymous]
        public ActionResult Mobile()
        {
            return View();
        }
        #endregion

        #region 实名认证
        [Route("~/my/realnameauth")]
        [AllowAnonymous]
        public ActionResult RealNameAuth()
        {
            return View();
        }
        #endregion

        #region Get Action

        #endregion

        #region Post Action
        #region Update Nick  Name
        [Route("~/updateNickName")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetNickName(string nickName)
        {
            var result = FCJsonResult.CreateFailResult(this.Lang("Unable to update your nick name. Please try again."));
            if (!string.IsNullOrEmpty(nickName.NullSafe()))
            {
                try
                {
                    var cmd = new UserSetNickName(this.CurrentUser.UserID, nickName);
                    this.CommandBus.Send(cmd);
                    this.CurrentUser.LoginName = nickName;

                    result = FCJsonResult.CreateSuccessResult(this.Lang("Nickname updated successfully."));
                }
                catch (CommandExecutionException ex)
                {
                    Log.Error("Action SetNickName Error", ex);
                }
            }
            return Json(result);
        }
        #endregion

        #region Modify Login Password

        [Route("~/modifyPassword")]
        [HttpPost]
        public ActionResult ModifyLoginPassword(string oldpassword, string newpassword, string confirmpassword)
        {
            var result = FCJsonResult.CreateFailResult(this.Lang("Unable to update your login password. Please try again."));

            if (oldpassword.Length >= 6 && newpassword.Length >= 6 && confirmpassword == newpassword)
            {
                try
                {
                    var cmd = new UserModifyPassword(this.CurrentUser.UserID, oldpassword, newpassword);
                    this.CommandBus.Send(cmd);

                    result = FCJsonResult.CreateSuccessResult(this.Lang("Login password updated successfully."));
                }
                catch (CommandExecutionException ex)
                {
                    if (ex.ErrorCode == (int)ErrorCode.SMSPasswordError)
                        result = FCJsonResult.CreateFailResult(this.Lang("Unable to update your login password. Your Sms Authenticator code error."));
                    if (ex.ErrorCode == (int)ErrorCode.GAPasswordError)
                        result = FCJsonResult.CreateFailResult(this.Lang("Unable to update your login password. Your Google Authenticator code error."));
                    else if (ex.ErrorCode == (int)ErrorCode.OldPasswordError)
                        result = FCJsonResult.CreateFailResult(this.Lang("Unable to update your login password. Your old password error."));
                    else
                        Log.Error("Action ModifyLoginPassword Error", ex);
                }
            }

            return Json(result);
        }
        #endregion

        #region Modify Trade Password

        [Route("~/my/modifypaypwd")]
        [HttpPost]
        public ActionResult ModifyTradePassword(string oldPayPassword, string newPayPassword, string confirmPayPassword)
        {
            var result = FCJsonResult.CreateFailResult(this.Lang("Unable to update your trade password. Please try again."));

            if (oldPayPassword.Length >= 6 && newPayPassword.Length >= 6 && confirmPayPassword == newPayPassword)
            {
                try
                {
                    var cmd = new UserModifyTradePassword(this.CurrentUser.UserID, oldPayPassword, newPayPassword);
                    this.CommandBus.Send(cmd);
                    //如果资金密码之前没设置，现在填入一个随机串，可以判断已设置资金密码，且不会有泄露密码的风险
                    this.CurrentUser.TradePassword = Guid.NewGuid().Shrink();

                    result = FCJsonResult.CreateSuccessResult(this.Lang("Trade password updated successfully."));
                }
                catch (CommandExecutionException ex)
                {
                    //if (ex.ErrorCode == (int)ErrorCode.GAPasswordError)
                    //    result = FCJsonResult.CreateFailResult(this.Lang("Unable to update your trade password. Your Google Authenticator code error."));
                    //else if (ex.ErrorCode == (int)ErrorCode.SMSPasswordError)
                    //    result = FCJsonResult.CreateFailResult(this.Lang("Unable to update your trade password. Your Sms Authenticator code error."));
                    //else 
                    if (ex.ErrorCode == (int)ErrorCode.OldTradePasswordError)
                        result = FCJsonResult.CreateFailResult(this.Lang("Unable to update your trade password. Your old trade password error."));
                    else
                        Log.Error("Action ModifyTradePassword Error", ex);
                }
            }
            return Json(result);
        }
        #endregion

        #region Two-Factor

        #region Enable  Google Authentication
        [Route("~/enableGoogleAuthentication")]
        [HttpPost]
        public ActionResult EnableGoogleAuthentication()
        {
            var secretKey = Utilities.GenerateOTPKey();
            var content = string.Format("otpauth://totp/{0}?secret={1}", "DotPay:" + this.CurrentUser.LoginName, secretKey);
            QrEncoder enc = new QrEncoder(ErrorCorrectionLevel.H);
            var code = enc.Encode(content);
            var imageData = string.Empty;
            GraphicsRenderer r = new GraphicsRenderer(new FixedModuleSize(5, QuietZoneModules.Two), Brushes.Black, Brushes.White);

            using (MemoryStream ms = new MemoryStream())
            {
                r.WriteToStream(code.Matrix, ImageFormat.Png, ms);

                byte[] image = ms.ToArray();
                //otpauth://totp/MY_APP_LABEL?secret={0}
                imageData = Convert.ToBase64String(image);
                //return html.Raw(string.Format(@"<img src=""data:image/png;base64,{0}"" alt=""{1}"" />", Convert.ToBase64String(image), content));
            }
            var result = FCJsonResult.CreateFailResult(Language.LangHelpers.Lang("Unable to update your nick name. Please try again."));

            return Json(new { Code = 1, @ImageData = imageData, SecretKey = secretKey });
        }
        #endregion

        #region Bind google authentication
        [Route("~/bindga")]
        [HttpPost]
        public ActionResult BindGoogleAuthentication(string secretkey, string otpverify)
        {
            var result = FCJsonResult.CreateFailResult(this.Lang("Unable to verify Google Authentication code. Please try again."));
            try
            {
                var cmd = new UserOpenGoogleAuthentication(this.CurrentUser.UserID, secretkey, otpverify);
                this.CommandBus.Send(cmd);
                this.CurrentUser.TwoFactorFlg |= 11;

                result = FCJsonResult.CreateSuccessResult(this.Lang("Enable Google Authenticator successfully.Your Login2FA enabled by default."));
            }
            catch (CommandExecutionException ex)
            {
                Log.Error("Action BindGoogleAuthentication Error", ex);
            }
            return Json(result);
        }

        #endregion

        #region Unbind google authentication
        [Route("~/unbindga")]
        [HttpPost]
        public ActionResult UnbindGoogleAuthentication(string otpverify)
        {
            var result = FCJsonResult.CreateFailResult(this.Lang("Unable to verify Google Authentication code. Please try again."));
            try
            {
                var cmd = new UserCloseGoogleAuthentication(this.CurrentUser.UserID, otpverify);
                this.CommandBus.Send(cmd);
                this.CurrentUser.TwoFactorFlg &= 20;

                result = FCJsonResult.CreateSuccessResult(this.Lang("Disable Google Authenticator successfully."));
            }
            catch (CommandExecutionException ex)
            {
                if (ex.ErrorCode != (int)ErrorCode.GAPasswordError)
                    Log.Error("Action BindGoogleAuthentication Error", ex);
            }
            return Json(result);
        }

        #endregion

        #region Enable user mobile number
        [Route("~/enablemobile")]
        [HttpPost]
        public ActionResult EnableMobile()
        {
            return Json(new { Code = 1, SecretKey = Utilities.GenerateOTPKey() });
        }

        #endregion

        #region Bind Mobile
        [Route("~/bingmobile")]
        [HttpPost]
        public ActionResult BindSMSAuthentication(string mobile, string otpverify)
        {
            var result = FCJsonResult.CreateFailResult(this.Lang("Unknow Exception,Please refresh the page and try again"));
            try
            {
                if (Session["BindSMSSecretCode"] != null)
                {
                    var sms_otpkey = Session["BindSMSSecretCode"].ToString();
                    var cmd = new UserSetMobile(this.CurrentUser.UserID, mobile, sms_otpkey, otpverify);
                    this.CommandBus.Send(cmd);
                    this.CurrentUser.TwoFactorFlg |= 20;
                    this.CurrentUser.Mobile = mobile;

                    result = FCJsonResult.CreateSuccessResult(this.Lang("Disable Sms Authenticator successfully."));
                }
            }
            catch (CommandExecutionException ex)
            {
                if (ex.ErrorCode == (int)ErrorCode.SMSPasswordError)
                    result = FCJsonResult.CreateFailResult(this.Lang("Unable to update your mobile. Your Sms Authenticator code error."));
                if (ex.ErrorCode == (int)ErrorCode.MobileHasSet)
                    result = FCJsonResult.CreateFailResult(this.Lang("Your Mobile set yet,don't need set again."));
                else
                    Log.Error("Action bindSMSAuthentication Error", ex);
            }
            return Json(result);
        }

        #endregion

        #region Switch Login 2FA
        [Route("~/enableLogin2FA")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EnableLogin2FA(string ga_otp, string sms_otp)
        {
            var result = FCJsonResult.CreateFailResult(this.Lang("Unknow Exception,Please refresh the page and try again"));
            try
            {
                var cmd = new UserOpenLoginTwoFactor(this.CurrentUser.UserID, sms_otp, ga_otp);
                this.CommandBus.Send(cmd);
                result = FCJsonResult.CreateSuccessResult(this.Lang("Enable Login2FA successfully."));
            }
            catch (CommandExecutionException ex)
            {
                if (ex.ErrorCode == (int)ErrorCode.SMSPasswordError)
                    result = FCJsonResult.CreateFailResult(this.Lang("Unable to switch your Login-2FA. Your Sms Authenticator code error."));
                else if (ex.ErrorCode == (int)ErrorCode.GAPasswordError)
                    result = FCJsonResult.CreateFailResult(this.Lang("Unable to switch your Login-2FA. Your Google Authenticator code error."));
                else
                    Log.Error("Action EnableLogin2FA Error", ex);
            }
            return Json(result);
        }
        [Route("~/disableLogin2FA")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DisableLogin2FA(string sms_otp, string ga_otp)
        {
            var result = FCJsonResult.CreateFailResult(this.Lang("Unknow Exception,Please refresh the page and try again"));
            try
            {
                var cmd = new UserCloseLoginTwoFactor(this.CurrentUser.UserID, sms_otp, ga_otp);
                this.CommandBus.Send(cmd);
                result = FCJsonResult.CreateSuccessResult(this.Lang("Disable Login2FA successfully."));
            }
            catch (CommandExecutionException ex)
            {
                if (ex.ErrorCode == (int)ErrorCode.SMSPasswordError)
                    result = FCJsonResult.CreateFailResult(this.Lang("Unable to switch your Login-2FA. Your Sms Authenticator code error."));
                else if (ex.ErrorCode == (int)ErrorCode.GAPasswordError)
                    result = FCJsonResult.CreateFailResult(this.Lang("Unable to switch your Login-2FA. Your Google Authenticator code error."));
                else
                    Log.Error("Action EnableLogin2FA Error", ex);
            }
            return Json(result);
        }
        #endregion
        #endregion

        #region Identity Verifation
        [Route("~/identityVerifation")]
        [HttpPost]
        public ActionResult RealNameAuthentication(string realname, string identityno, IdNoType idNoType = IdNoType.IdentificationCard)
        {
            var result = FCJsonResult.CreateFailResult(this.Lang("Unable to update your identity informations. Please try again."));

            if (!string.IsNullOrEmpty(realname.NullSafe()) && !string.IsNullOrEmpty(identityno.NullSafe()))
            {
                if (idNoType == IdNoType.IdentificationCard && (identityno.NullSafe().Length >= 6 && identityno.NullSafe().Length <= 18))
                {
                    try
                    {
                        var cmd = new UserRealNameAuth(this.CurrentUser.UserID, realname, idNoType, identityno);
                        this.CommandBus.Send(cmd);
                        this.CurrentUser.IdNoType = idNoType;
                        this.CurrentUser.IdNo = identityno;
                        this.CurrentUser.RealName = realname;
                        result = FCJsonResult.CreateSuccessResult(this.Lang("Identity information updated successfuly."));
                    }
                    catch (CommandExecutionException ex)
                    {
                        if (ex.ErrorCode == (int)ErrorCode.RealNameAuthenticationIsPassed)
                            result = FCJsonResult.CreateFailResult(this.Lang("Identity informations have updated yet! Please refresh the page to see your identity infomations."));
                        else
                            Log.Error("Action RealNameAuthentication Error", ex);
                    }
                }
            }
            return Json(result);
        }

        #endregion

        #region QueryTransactionData
        [Route("~/querytx")]
        [HttpPost]
        public ActionResult RealNameAuthentication(int page)
        {
            var result = FCJsonResult.UnknowFail;

            try
            {
                var data = IoC.Resolve<ITransactionQuery>().GetUserTransactions(this.CurrentUser.UserID, 20, page);

                return Json(new { Code = 1, Data = data });
            }

            catch
            {
                return Json(result);
            }
        }
        #endregion

        #endregion
        #endregion

        #region Balances
        #region Views
        [Route("~/balances")]
        public ActionResult Balances()
        {
            ViewBag.UserAccounts = IoC.Resolve<IAccountQuery>().GetAccountsByUserID(this.CurrentUser.UserID);
            return View();
        }
        #endregion
        #endregion
    }
}
