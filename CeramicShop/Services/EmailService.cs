using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using CeramicShop.Models;

namespace CeramicShop.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string email, string subject, string message);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            try
            {
                var mimeMessage = new MimeMessage();
                mimeMessage.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
                mimeMessage.To.Add(new MailboxAddress("", email));
                mimeMessage.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = message
                };

                mimeMessage.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    // Kết nối đến SMTP server
                    await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.Port, SecureSocketOptions.StartTls);

                    // Xác thực với SMTP server
                    await client.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.Password);

                    // Gửi email
                    await client.SendAsync(mimeMessage);

                    // Ngắt kết nối
                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                // Ghi log lỗi
                Console.WriteLine($"Lỗi khi gửi email: {ex.Message}");
                throw new Exception($"Lỗi khi gửi email: {ex.Message}");
            }
        }
    }
}
