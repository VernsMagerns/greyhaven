using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using GreyHaven.Presentation;

namespace GreyHaven.App.Infrastructure;

/// <summary>
/// Plays GreyHaven's quiet sound effects (assets/sounds, made by tools/sounds/build.py, built into
/// the app). Ticking a checkbox ticks; the rest come from the view models. Off when the person
/// turns sounds off. A sound that can't play is skipped: sounds are never needed to use the app.
/// </summary>
public sealed class SoundEffects
{
    private readonly Dictionary<SoundCue, SoundPlayer> _players = new();

    public SoundEffects()
    {
        foreach (var cue in Enum.GetValues<SoundCue>())
        {
            try
            {
                var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/Sounds/{cue.ToString().ToLowerInvariant()}.wav"))?.Stream;
                if (stream is null) continue;
                var player = new SoundPlayer(stream);
                player.Load();
                _players[cue] = player;
            }
            catch (Exception ex) when (ex is System.IO.IOException or InvalidOperationException or TimeoutException) { }
        }
        // Only ticks the person makes (mouse or keyboard), not boxes the app ticks itself.
        EventManager.RegisterClassHandler(typeof(CheckBox), ToggleButton.CheckedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is CheckBox { IsMouseOver: true } or CheckBox { IsKeyboardFocused: true }) Play(SoundCue.Tick);
        }));
    }

    public bool Enabled { get; set; } = true;

    public void Play(SoundCue cue)
    {
        if (!Enabled || !_players.TryGetValue(cue, out var player)) return;
        try { player.Play(); }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException) { }
    }
}
