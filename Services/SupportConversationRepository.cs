using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class SupportConversationRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    public string GetStoragePath(AuthenticatedUser user)
    {
        var identifier = string.IsNullOrWhiteSpace(user.Email) ? user.Username : user.Email;
        var safeIdentifier = string.Join(
            string.Empty,
            identifier.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelCrmDemo",
            "support",
            safeIdentifier + ".json");
    }

    public IReadOnlyList<SupportMessageRow> LoadConversation(AuthenticatedUser user)
    {
        var storagePath = GetStoragePath(user);

        if (!File.Exists(storagePath))
        {
            var seedMessages = CreateSeedMessages(user);
            SaveConversation(user, seedMessages);
            return seedMessages;
        }

        try
        {
            var json = File.ReadAllText(storagePath);
            var store = JsonSerializer.Deserialize<SupportConversationStore>(json, SerializerOptions);

            if (store?.Messages is { Count: > 0 })
            {
                return store.Messages.OrderBy(message => message.CreatedAt).ToList();
            }
        }
        catch
        {
        }

        var fallbackMessages = CreateSeedMessages(user);
        SaveConversation(user, fallbackMessages);
        return fallbackMessages;
    }

    public void SaveConversation(AuthenticatedUser user, IEnumerable<SupportMessageRow> messages)
    {
        var storagePath = GetStoragePath(user);
        var directory = Path.GetDirectoryName(storagePath)
            ?? throw new InvalidOperationException("Support conversation path is invalid.");

        Directory.CreateDirectory(directory);

        var store = new SupportConversationStore
        {
            Messages = messages.OrderBy(message => message.CreatedAt).ToList()
        };

        var json = JsonSerializer.Serialize(store, SerializerOptions);
        File.WriteAllText(storagePath, json);
    }

    public SupportMessageRow CreateUserMessage(AuthenticatedUser user, string message)
        => new SupportMessageRow
        {
            IsFromUser = true,
            SenderName = user.DisplayName,
            Body = message.Trim(),
            CreatedAt = DateTime.Now,
            Channel = DetectChannel(message),
            IsUrgent = IsUrgent(message)
        };

    public SupportMessageRow CreateAutomatedReply(AuthenticatedUser user, string message)
    {
        var channel = DetectChannel(message);
        var urgent = IsUrgent(message);

        return new SupportMessageRow
        {
            IsFromUser = false,
            SenderName = urgent ? "Titan Priority Support" : "Titan Support",
            Body = BuildReplyBody(user, channel, urgent),
            CreatedAt = DateTime.Now.AddSeconds(1),
            Channel = channel,
            IsUrgent = urgent
        };
    }

    private static List<SupportMessageRow> CreateSeedMessages(AuthenticatedUser user)
    {
        var now = DateTime.Now;

        return new List<SupportMessageRow>
        {
            new SupportMessageRow
            {
                IsFromUser = false,
                SenderName = "Titan Support",
                Body = $"Hi {user.DisplayName}. This support inbox is available 24/7 for automated triage on billing, access, contracts, scheduling, and release questions.",
                CreatedAt = now.AddMinutes(-9),
                Channel = "Welcome",
                IsUrgent = false
            },
            new SupportMessageRow
            {
                IsFromUser = false,
                SenderName = "Titan Support",
                Body = "Start a message below and Titan will answer instantly while saving the transcript locally for follow-up.",
                CreatedAt = now.AddMinutes(-8),
                Channel = "Welcome",
                IsUrgent = false
            }
        };
    }

    private static string DetectChannel(string message)
    {
        var normalized = message.ToLowerInvariant();

        if (ContainsAny(normalized, "password", "login", "sign in", "signin", "access", "locked out"))
        {
            return "Access";
        }

        if (ContainsAny(normalized, "bill", "billing", "payment", "invoice", "charge", "refund"))
        {
            return "Billing";
        }

        if (ContainsAny(normalized, "calendar", "schedule", "meeting", "call", "reminder"))
        {
            return "Calendar";
        }

        if (ContainsAny(normalized, "contract", "agreement", "split", "signature"))
        {
            return "Contracts";
        }

        if (ContainsAny(normalized, "release", "launch", "post", "social", "spotify", "youtube", "facebook"))
        {
            return "Launch";
        }

        return "General";
    }

    private static bool IsUrgent(string message)
    {
        var normalized = message.ToLowerInvariant();
        return ContainsAny(normalized, "urgent", "asap", "immediately", "cant", "can't", "down", "broken", "failed");
    }

    private static string BuildReplyBody(AuthenticatedUser user, string channel, bool urgent)
    {
        var prefix = urgent
            ? "Priority routing is active. "
            : "Your request has been logged. ";

        return channel switch
        {
            "Access" => prefix + "Start by confirming the email or username tied to this device, then retry sign-in. If access still fails, reset the local credential entry from the account workspace after backup.",
            "Billing" => prefix + "Open the payments workspace to review the latest charge dates and invoice cadence. If a charge looks wrong, note the invoice month and amount here so it stays attached to the transcript.",
            "Calendar" => prefix + "Use the calendar workspace to confirm the upcoming task or call, then send the exact date and title that needs attention. Titan can keep the scheduling context in this thread for follow-up.",
            "Contracts" => prefix + "Open the contracts workspace and reference the client or agreement name in your next message. That keeps the issue narrowed to the right draft, signature step, or split setup.",
            "Launch" => prefix + "Share the release or post name plus the platform involved, and Titan will keep the launch support notes in one thread. The dashboard can stay open while you coordinate the next step.",
            _ => prefix + $"Titan is standing by for {user.DisplayName}. Send a little more detail about what is blocked and the support lane will classify it automatically."
        };
    }

    private static bool ContainsAny(string value, params string[] fragments)
        => fragments.Any(value.Contains);

    private sealed class SupportConversationStore
    {
        public List<SupportMessageRow> Messages { get; set; } = new List<SupportMessageRow>();
    }
}
