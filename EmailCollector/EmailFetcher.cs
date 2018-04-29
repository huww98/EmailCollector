using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Data;
using EmailCollector.Models;
using Huww98.Data;
using MailKit;
using MailKit.Net.Imap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EmailCollector
{
    public class EmailFetcherOptions
    {
        public string ImapServer { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string AttachmentSavePath { get; set; }
    }

    public class EmailFetcher
    {
        private readonly EmailFetcherOptions options;
        private readonly EmailCollectorContext context;
        private readonly ILogger<EmailFetcher> logger;

        public EmailFetcher(IOptions<EmailFetcherOptions> options, EmailCollectorContext context, ILogger<EmailFetcher> logger)
        {
            this.options = options.Value;
            this.context = context;
            this.logger = logger;
        }

        ImapClient client = new ImapClient();
        CancellationTokenSource doneCts = new CancellationTokenSource();
        ConcurrentQueue<Func<CancellationToken, Task>> pendingCommands = new ConcurrentQueue<Func<CancellationToken, Task>>();
        SHA1 sha1 = SHA1.Create();

        MessageSummaryItems summaryItems = MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure;

        void IssueCommandOnIdle(Func<CancellationToken, Task> command)
        {
            pendingCommands.Enqueue(command);
            doneCts.Cancel();
        }

        async Task ProcessNewEmailAsync(IMessageSummary summary, CancellationToken ct)
        {
            var subject = summary.Envelope.Subject;
            logger.LogInformation("New Email arrived. subject: {0}, uid: {1}", subject, summary.UniqueId.Id);
            if (subject == null)
            {
                logger.LogInformation("Uid: {0} process failed, no subject.", summary.UniqueId.Id);
                return;
            }
            if (!(subject.Contains("操作系统") && subject.Contains("实验一")))
            {
                logger.LogInformation("Uid: {0} process failed, subject not match", summary.UniqueId.Id);
                return;
            }
            var student = Student.AllStudents.SingleOrDefault(
                s => subject.Contains(s.Name) ||
                     summary.Envelope.From.Any(a=>a.Name.Contains(s.Name)) ||
                     summary.Attachments.Any(a=>a.FileName.Contains(s.Name)));
            if (student == null)
            {
                logger.LogInformation("Uid: {0} process failed, no student found", summary.UniqueId.Id);
                return;
            }

            var attachmentMetadata = summary.Attachments.FirstOrDefault(a => KnownHomeworkExtensions.IsPathHasKnownExtension(a.FileName));
            if (attachmentMetadata == null)
            {
                logger.LogInformation("Uid: {0} process failed, no attachment found", summary.UniqueId.Id);
                return;
            }
            var attachment = (MimePart) await client.Inbox.GetBodyPartAsync(summary.UniqueId, attachmentMetadata, ct);
            Email email = new Email
            {
                EmailUID = summary.UniqueId.Id,
                AttachmentName = attachment.FileName,
                SenderName = student.Name,
                FromAddress = summary.Envelope.From.OfType<MailboxAddress>().First().Address,
            };

            MemoryStream content = new MemoryStream();
            await attachment.Content.DecodeToAsync(content);

            content.Position = 0;
            email.AttachmentSHA1 = sha1.ComputeHash(content);
            content.Position = 0;
            if (options.AttachmentSavePath != null)
            {
                email.FileSavePath = Path.GetFullPath(Path.Combine(options.AttachmentSavePath, $"{student.StudentNumber}-{student.Name}{Path.GetExtension(attachment.FileName)}"));
                using (var file = File.OpenWrite(email.FileSavePath))
                {
                    content.CopyTo(file);
                }
            }
            context.Email.Add(email);
            await context.SaveChangesAsync();
            logger.LogInformation("Uid: {0} process success", summary.UniqueId.Id);
        }

        async Task FetchNewEmailsAsync(CancellationToken ct)
        {
            uint lastUid;
            if (await context.Email.CountAsync() == 0)
            {
                lastUid = 0;
            }
            else
            {
                lastUid = await context.Email.MaxAsync(e => e.EmailUID);
            }
            var uids = new UniqueIdRange(new UniqueId(lastUid + 1), UniqueId.MaxValue);
            foreach (var message in await client.Inbox.FetchAsync(uids, summaryItems, ct))
            {
                await ProcessNewEmailAsync(message, ct);
            }
        }

        public async Task FetchEmailAsync(CancellationToken ct)
        {
            ct.Register(() => doneCts.Cancel());
            Directory.CreateDirectory(options.AttachmentSavePath);
            await client.ConnectAsync(options.ImapServer, 993, true, ct);
            await client.AuthenticateAsync(options.UserName, options.Password, ct);
            await client.Inbox.OpenAsync(FolderAccess.ReadOnly, ct);

            var localUids = context.Email.Select(e => e.EmailUID).ToList();
            foreach (var message in await client.Inbox.FetchAsync(0, -1, summaryItems, ct))
            {
                if (!localUids.Contains(message.UniqueId.Id))
                {
                    await ProcessNewEmailAsync(message, ct);
                }
            }

            client.Inbox.CountChanged += (sender, e) =>
            {
                IssueCommandOnIdle(ct1 => FetchNewEmailsAsync(ct1));
            };
            logger.LogInformation("EmailFetcher initialized");
            while (true)
            {
                doneCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                await client.IdleAsync(doneCts.Token);
                ct.ThrowIfCancellationRequested();
                while(pendingCommands.TryDequeue(out var cmd))
                {
                    await cmd(ct);
                }
            }
        }
    }
}
