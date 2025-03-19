using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Equinor.ProCoSys.Common.Email;

public class EmailService(IOptionsMonitor<EmailOptions> emailOptions, TokenCredential emailTokenCredential, ILogger<EmailService> logger) : IEmailService
{
    private readonly string _mailUserOid = emailOptions.CurrentValue.MailUserOid;
    
    public async Task SendEmailsAsync(List<string> emails, string subject, string body, CancellationToken cancellationToken = default)
    {
        EmailValidator.ValidateEmails(emails);

        var graphServiceClient = new GraphServiceClient(emailTokenCredential);
        var graphMessage = new Message
        {
            Subject = subject,
            Body = new ItemBody
            {
                ContentType = BodyType.Html,
                Content = body
            },
            ToRecipients = emails.Select(x => new Recipient { EmailAddress = new EmailAddress { Address = x } }).ToList()
        };
        try
        {
            await graphServiceClient.Users[_mailUserOid]
                   .SendMail
                   .PostAsync(new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                   {
                       Message = graphMessage,
                       SaveToSentItems = false                           
                   }, null, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, $"An email with subject {subject} could not be sent.");
            throw new Exception($"It was not possible to send an email (subject: {subject})", ex);
        }
    }

    public async Task SendMessageAsync(Message graphMessage,
        CancellationToken cancellationToken = default)
    {
        if (graphMessage == null)
        {
            throw new ArgumentNullException(nameof(graphMessage));
        }
        if (graphMessage.ToRecipients == null || !graphMessage.ToRecipients.Any())
        {
            logger.LogInformation("Tried to send mail without any recipients.");
            throw new Exception("It was not possible to send an email since it does not contain any recipients.");
        }
        if (graphMessage.ToRecipients.Any(x => x.EmailAddress?.Address == null))
        {
            logger.LogInformation("Tried to send mail to recipients without defining any address.");
            throw new Exception("It was not possible to send an email since it does not contain any address for one or more recipients.");
        }

        EmailValidator.ValidateEmails(graphMessage.ToRecipients.Select(x => x.EmailAddress?.Address).ToList());

        var graphServiceClient = new GraphServiceClient(emailTokenCredential);

        await graphServiceClient.Users[_mailUserOid]
            .SendMail
            .PostAsync(new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody() {
                Message = graphMessage, 
                SaveToSentItems = false 
            }, null, cancellationToken);
    }
}

