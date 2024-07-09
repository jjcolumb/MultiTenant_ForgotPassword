using MultiTenant_ForgotPassword.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp;
using DevExpress.Persistent.Base.Security;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using DevExpress.Persistent.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DevExpress.ExpressApp.MultiTenancy;
using DevExpress.Persistent.BaseImpl.MultiTenancy;
using DevExpress.Xpo;
using DevExpress.ExpressApp.Xpo;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.Xpo.DB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MultiTenant_ForgotPassword.Module.Security
{
    [DomainComponent]
    public class MessageParameters
    {
        [ModelDefault("AllowEdit", "False")]
        public string Message { get; set; }
    }


    [DomainComponent]
    public abstract class LogonActionParametersBase
    {
        public const string EmailPattern = @"^[_a-z0-9-]+(\.[_a-z0-9-]+)*@[a-z0-9-]+(\.[a-z0-9-]+)*(\.[a-z]{2,4})$";
        public const string ValidationContext = "RegisterUserContext";

        [RuleRequiredField(null, ValidationContext)]
        //[RuleRegularExpression(null, ValidationContext, EmailPattern, "Must be a valid Email")]
        public string UserName { get; set; }

        public abstract void ExecuteBusinessLogic(XafApplication Application);
    }


    [DomainComponent]
    [ModelDefault("Caption", "Restore Password")]
    [ImageName("Action_ResetPassword")]
    public class RestorePasswordParameters : LogonActionParametersBase
    {
        private IConfiguration Configuration { get; set; }

        [Browsable(false)]
        public bool UserNotFound { get; set; }

        [Browsable(false)]
        public bool EmailNotFound { get; set; }

        public override void ExecuteBusinessLogic(XafApplication Application)
        {
            var parts = UserName.Split('@');
            if(parts.Length == 2)
            {
                string tenantName = parts[1];

                Configuration = Application.ServiceProvider.GetRequiredService<IConfiguration>();
                string connectionString = Configuration["ConnectionStrings:ConnectionString"];
                XpoDefault.DataLayer = XpoDefault.GetDataLayer(connectionString, AutoCreateOption.DatabaseAndSchema);

                Tenant tenant;
                using(UnitOfWork UnitOWork = new UnitOfWork())
                {
                    tenant = UnitOWork.Query<Tenant>().Where(t => t.Name == tenantName).First();
                }

                Application.ConnectionString = tenant.ConnectionString;
            }

            var objectSpace = Application.CreateObjectSpace(typeof(ApplicationUser));
            ApplicationUser tempuser = objectSpace.FindObject<ApplicationUser>(
                CriteriaOperator.Parse("UserName = ?", UserName));

            var user = tempuser as IAuthenticationStandardUser;

            if(user == null)
            {
                UserNotFound = true;
                return;
            }

            if(!string.IsNullOrEmpty(tempuser.EmailAddress))
            {
                byte[] randomBytes = new byte[6];
                new RNGCryptoServiceProvider().GetBytes(randomBytes);
                string password = Convert.ToBase64String(randomBytes);
                user.SetPassword(password);
                user.ChangePasswordOnFirstLogon = true;
                objectSpace.CommitChanges();
                EmailLoginInformation(tempuser.EmailAddress, password, user.UserName);
            }
            else
            {
                EmailNotFound = true;
                return;
            }
                
        }

        public static void EmailLoginInformation(string emailAddress, string pwd, string username)
        {
            var fromAddress = "hectorjavier9421@outlook.com";
            var toAddress = emailAddress;
            string fromPassword = "Debian12345";

            string subject = "Password reset request";
            string body = "Hello " +
                username +
                "," +
                Environment.NewLine +
                Environment.NewLine +
                "A password reset request was made for your user. Here is your new Password " +
                pwd +
                "." +
                Environment.NewLine +
                Environment.NewLine +
                "Please use the link provided below to log in and you will be prompted to create a new password." +
                Environment.NewLine +
                Environment.NewLine +
                "Link to your app" +
                Environment.NewLine +
                Environment.NewLine +
                "This is an automated response acknowledging your request. Please do not reply to this e-mail.";
            var smtp = new SmtpClient
            {
                Host = "smtp.office365.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = new NetworkCredential(fromAddress, fromPassword),
                Timeout = 20000
            };
            using(var message = new MailMessage(fromAddress, toAddress) { Subject = subject, Body = body ?? " " })
            {
                try
                {
                    message.IsBodyHtml = false;
                    smtp.Send(message);
                } catch(Exception ex)
                {
                }
            }
        }
    }
}
