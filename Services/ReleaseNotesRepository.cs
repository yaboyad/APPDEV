using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class ReleaseNotesRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string notesPath;

    public ReleaseNotesRepository(string? notesPath = null)
    {
        this.notesPath = notesPath ?? ResolveNotesPath();
    }

    public string NotesPath => notesPath;

    public IReadOnlyList<LoginReleaseNote> Load()
    {
        try
        {
            if (File.Exists(notesPath))
            {
                var json = File.ReadAllText(notesPath);
                var notes = JsonSerializer.Deserialize<List<LoginReleaseNote>>(json, SerializerOptions);
                var normalized = Normalize(notes);

                if (normalized.Count > 0)
                {
                    return normalized;
                }
            }
        }
        catch (Exception)
        {
        }

        return CreateFallbackNotes();
    }

    private static string ResolveNotesPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "LoginReleaseNotes.json"),
            Path.Combine(Environment.CurrentDirectory, "Assets", "LoginReleaseNotes.json")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    private static List<LoginReleaseNote> Normalize(IEnumerable<LoginReleaseNote>? notes)
    {
        if (notes is null)
        {
            return new List<LoginReleaseNote>();
        }

        var normalized = new List<LoginReleaseNote>();

        foreach (var note in notes)
        {
            var highlights = (note.Highlights ?? new List<string>())
                .Where(highlight => !string.IsNullOrWhiteSpace(highlight))
                .Select(highlight => highlight.Trim())
                .ToList();

            if (string.IsNullOrWhiteSpace(note.Version)
                && string.IsNullOrWhiteSpace(note.Title)
                && string.IsNullOrWhiteSpace(note.Summary)
                && highlights.Count == 0)
            {
                continue;
            }

            normalized.Add(new LoginReleaseNote
            {
                Version = string.IsNullOrWhiteSpace(note.Version) ? "Upcoming" : note.Version.Trim(),
                Title = string.IsNullOrWhiteSpace(note.Title) ? "Platform update" : note.Title.Trim(),
                PublishedOn = string.IsNullOrWhiteSpace(note.PublishedOn) ? "Local build" : note.PublishedOn.Trim(),
                Summary = note.Summary.Trim(),
                Highlights = highlights
            });
        }

        return normalized;
    }

    private static IReadOnlyList<LoginReleaseNote> CreateFallbackNotes() =>
        new List<LoginReleaseNote>
        {
            new LoginReleaseNote
            {
                Version = "v1.3",
                Title = "Fullscreen shell refresh",
                PublishedOn = "March 11, 2026",
                Summary = "The login experience now behaves more like a living product surface instead of a one-off splash screen.",
                Highlights = new List<string>
                {
                    "The shell stays maximized while standard minimize and close controls remain available.",
                    "The login note area supports stacked release history so new features do not replace older updates."
                }
            },
            new LoginReleaseNote
            {
                Version = "v1.2",
                Title = "Workspace launch polish",
                PublishedOn = "March 10, 2026",
                Summary = "Startup and dashboard handoff were tuned to feel more intentional from the first second.",
                Highlights = new List<string>
                {
                    "The workspace opens maximized immediately after sign-in.",
                    "The main shell keeps the artist CRM layout sharp across larger desktop sizes."
                }
            },
            new LoginReleaseNote
            {
                Version = "v1.1",
                Title = "Local account protection",
                PublishedOn = "March 10, 2026",
                Summary = "Credential storage was moved into a protected local vault for the test environment.",
                Highlights = new List<string>
                {
                    "A credential vault is created on first launch for the current Windows account.",
                    "Passwords stay hashed and the local store remains encrypted on disk."
                }
            }
        };
}
