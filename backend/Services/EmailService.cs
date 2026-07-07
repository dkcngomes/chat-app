using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace backend.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendTranscriptAsync(string toEmail, string subject, string bodyHtml,
        List<string>? attachmentPaths = null)
    {
        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(_config["Email:From"]));
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;

        var multipart = new Multipart("mixed");

        // HTML body
        var htmlPart = new TextPart(TextFormat.Html) { Text = bodyHtml };
        multipart.Add(htmlPart);

        // Attachments
        if (attachmentPaths != null)
        {
            foreach (var path in attachmentPaths)
            {
                if (File.Exists(path))
                {
                    var attachment = new MimePart("image", Path.GetExtension(path).TrimStart('.'))
                    {
                        Content = new MimeContent(File.OpenRead(path)),
                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                        ContentTransferEncoding = ContentEncoding.Base64,
                        FileName = Path.GetFileName(path)
                    };
                    multipart.Add(attachment);
                }
            }
        }

        email.Body = multipart;

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_config["Email:Host"], int.Parse(_config["Email:Port"] ?? "587"),
            SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(_config["Email:Username"], _config["Email:Password"]);
        await smtp.SendAsync(email);
        await smtp.DisconnectAsync(true);
    }
}
