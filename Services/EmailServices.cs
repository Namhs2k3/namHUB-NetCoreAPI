using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string message);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string to, string subject, string message)
    {
        var smtpClient = new SmtpClient(_configuration["Smtp:Server"])
        {
            Port = int.Parse(_configuration["Smtp:Port"]),
            Credentials = new NetworkCredential(_configuration["Smtp:User"], _configuration["Smtp:Pass"]),
            EnableSsl = true // Bật SSL để gửi email qua kết nối bảo mật
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_configuration["Smtp:User"]),
            Subject = subject,
            Body = message,
            IsBodyHtml = true,
        };

        mailMessage.To.Add(to);

        await smtpClient.SendMailAsync(mailMessage);
    }
}

