using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

public interface IEmailService
{
    Task SendEmailAsync( string to, string subject, string message, string? link );

    Task SendEmailAsync( string to, string subject, string message );
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService( IConfiguration configuration )
    {
        _configuration = configuration;
    }

    //Phương Thức SendEmailAsync này là của Nam
    public async Task SendEmailAsync( string to, string subject, string message, string? link )
    {
        var smtpClient = new SmtpClient( _configuration["Smtp:Server"] )
        {
            Port = int.Parse( _configuration["Smtp:Port"] ),
            Credentials = new NetworkCredential( _configuration["Smtp:User"], _configuration["Smtp:Pass"] ),
            EnableSsl = true // Bật SSL để gửi email qua kết nối bảo mật
        };

        // Thêm CSS vào nội dung email
        string htmlMessage = $@"
                        <html>
                        <head>
                            <style>
                                body {{
                                    font-family: Arial, sans-serif;
                                    margin: 0;
                                    padding: 20px;
                                    background-color: #f4f4f4;
                                }}
                                .email-container {{
                                    background-color: #ffffff;
                                    border-radius: 5px;
                                    padding: 20px;
                                    text-align:center;
                                    box-shadow: 0px 4px 6px rgba(0, 0, 0, 0.1);
                                }}
                                h1 {{
                                    color: #333333;
                                    font-size: 24px;
                                    margin-bottom: 10px;
                                }}
                                .div-btn {{
                                    display:flex;
                                    align-items: center;
                                    justify-content:center;
                                    width: 100px;
                                    margin: 10px auto;
                                }}
                                .btn{{
                                    padding: 8px 15px;
                                    border: none;
                                    max-width: 45%;
                                    border-radius: 5px;
                                    font-size: 14px;
                                    font-weight: bold;
                                    cursor: pointer;
                                    background-color: #007bff;
                                    transition: background-color 0.3s, transform 0.2s;
                                    color: #fff !important;
                                    text-decoration:none;
                                }}
                            </style>
                        </head>
                        <body>
                            <div class='email-container'>
                                <h1>{subject}</h1>
                                <p>{message}</p>
                                <div class='div-btn'><a class='btn' href='{link}'>Verify<a/><div/>
                            </div>
                        </body>
                        </html>";
        var mailMessage = new MailMessage
        {
            From = new MailAddress( _configuration["Smtp:User"] ),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true,
        };

        mailMessage.To.Add( to );
        //Phương Thức SendEmailAsync này là của đối tượng SmtpClient
        await smtpClient.SendMailAsync( mailMessage );
    }

    public async Task SendEmailAsync( string to, string subject, string message )
    {
        var smtpClient = new SmtpClient( _configuration["Smtp:Server"] )
        {
            Port = int.Parse( _configuration["Smtp:Port"] ),
            Credentials = new NetworkCredential( _configuration["Smtp:User"], _configuration["Smtp:Pass"] ),
            EnableSsl = true // Bật SSL để gửi email qua kết nối bảo mật
        };

        // Thêm CSS vào nội dung email
        string htmlMessage = $@"
                            <html>
                            <head>
                                <style>
                                    body {{
                                        font-family: Arial, sans-serif;
                                        margin: 0;
                                        padding: 20px;
                                        background-color: #f4f4f4;
                                    }}
                                    .email-container {{
                                        background-color: #ffffff;
                                        border-radius: 5px;
                                        padding: 20px;
                                        box-shadow: 0px 4px 6px rgba(0, 0, 0, 0.1);
                                    }}
                                    h1 {{
                                        color: #333333;
                                        font-size: 24px;
                                        margin-bottom: 10px;
                                    }}
                                    p {{
                                        color: #555555;
                                        font-size: 16px;
                                        line-height: 1.5;
                                    }}
                                    .footer {{
                                        margin-top: 20px;
                                        font-size: 14px;
                                        color: #888888;
                                    }}
                                </style>
                            </head>
                            <body>
                                <div class='email-container'>
                                    <h1>{subject}</h1>
                                    <p>{message}</p>
                                    <div class='footer'>
                                        <p>This is an automated email, please do not reply.</p>
                                    </div>
                                </div>
                            </body>
                            </html>";

        var mailMessage = new MailMessage
        {
            From = new MailAddress( _configuration["Smtp:User"] ),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true,
        };

        mailMessage.To.Add( to );
        //Phương Thức SendEmailAsync này là của đối tượng SmtpClient
        await smtpClient.SendMailAsync( mailMessage );
    }
}