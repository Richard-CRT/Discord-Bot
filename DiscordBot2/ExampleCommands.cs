// THIS FILE IS A PART OF EMZI0767'S BOT EXAMPLES
//
// --------
// 
// Copyright 2019 Emzi0767
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// --------
//
// This is a commands example. It shows how to properly utilize 
// CommandsNext, as well as use its advanced functionality.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Net.Models;
using DSharpPlus.VoiceNext;

namespace DiscordBot2
{
    public class ExampleUngrouppedCommands : BaseCommandModule
    {
        [Command("ping")] // let's define this method as a command
        [Description("Example ping command")] // this will be displayed to tell users what this command does when they invoke help
        [Aliases("pong")] // alternative names for the command
        public async Task Ping(CommandContext ctx) // this command takes no arguments
        {
            // let's trigger a typing indicator to let
            // users know we're working
            await ctx.TriggerTypingAsync();

            // let's make the message a bit more colourful
            var emoji = DiscordEmoji.FromName(ctx.Client, ":ping_pong:");

            // respond with ping
            await ctx.RespondAsync($"{emoji} Pong! Ping: {ctx.Client.Ping}ms");
        }
    }

    public class AudioUngroupedCommands : BaseCommandModule
    {
        public AudioUngroupedCommands()
        {

        }

        [Command("join")] // let's define this method as a command
        [Description("Joins a voice channel.")]
        [Aliases("j")]
        public async Task Join(CommandContext ctx) // this command takes no arguments
        {
            // check whether VNext is enabled
            // null if VNext is not enabled or configured.
            var vnext = ctx.Client.GetVoiceNext();
            if (vnext == null)
            {
                return;
            }

            // get member's voice state
            var vstat = ctx.Member.VoiceState;
            if (vstat.Channel == null)
            {
                // they did not specify a channel and are not in one
                var clown_emoji = DiscordEmoji.FromName(ctx.Client, ":clown:");
                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"You're not in a voice channel {clown_emoji}");
                return;
            }

            var vnc = vnext.GetConnection(ctx.Guild);
            bool connected = vnc != null;
            bool in_right_channel = connected && vstat.Channel == vnc.TargetChannel;
            if (connected && !in_right_channel)
            {
                await Leave(ctx);
                connected = false;
            }

            if (!connected)
            {
                cancelPlayingTokenSource?.Cancel();
                // connect
                vnc = await vnext.ConnectAsync(vstat.Channel);
            }
        }

        [Command("leave")]
        [Description("Leaves a voice channel.")]
        [Aliases("l")]
        public async Task Leave(CommandContext ctx)
        {
            // check whether VNext is enabled
            // null if VNext is not enabled or configured.
            var vnext = ctx.Client.GetVoiceNext();
            if (vnext == null)
            {
                return;
            }

            // check whether we are connected
            var vnc = vnext.GetConnection(ctx.Guild);
            if (vnc == null)
            {
                // not connected
                return;
            }

            cancelPlayingTokenSource?.Cancel();

            // disconnect
            vnc.Disconnect();
        }


        public float VolumeLevel = 1;
        [Command("volume")]
        [Description("Adjust global volume of the bot")]
        [Aliases("v")]
        public async Task Volume(CommandContext ctx, [Description("New volume 1-100")] int new_volume)
        {
            await ctx.TriggerTypingAsync();
            int int_volume = Math.Max(5, Math.Min(100, new_volume));
            VolumeLevel = int_volume / (float)100;
            if (txStream != null)
                txStream.VolumeModifier = VolumeLevel;
            var speaker_emoji = DiscordEmoji.FromName(ctx.Client, ":speaker:");
            string toSend = $"`{speaker_emoji} {int_volume}% [";
            for (int i = 0; i < 100; i += 2)
            {
                if (i < int_volume)
                {
                    toSend += "=";
                }
                else
                {
                    toSend += " ";
                }
            }
            toSend += "]`";
            await ctx.RespondAsync(toSend);
        }

        CancellationTokenSource cancelPlayingTokenSource;
        VoiceTransmitSink txStream;
        [Command("play")]
        [Description("Plays an audio file")]
        [Aliases("p")]
        public async Task Play(CommandContext ctx, [Description("Name: Alphabet Eat Horny Munching Penetrate Pretty Showing Yabba")] string name)
        {
            await Join(ctx);

            // check whether VNext is enabled
            var vnext = ctx.Client.GetVoiceNext();
            if (vnext == null)
            {
                return;
            }

            // check whether we aren't already connected
            var vnc = vnext.GetConnection(ctx.Guild);
            if (vnc == null)
            {
                return;
            }

            name = name.ToLower();

            string filename;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                filename = "recordings\\" + name + ".mp3";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                filename = "./recordings/" + name + ".mp3";
            }
            else
            {
                return;
            }

            // check if file exists
            if (!File.Exists(filename))
            {
                // file does not exist
                //await ctx.RespondAsync($"File `{filename}` does not exist.");
                return;
            }

            // wait for current playback to finish
            cancelPlayingTokenSource?.Cancel();

            while (vnc.IsPlaying)
                await vnc.WaitForPlaybackFinishAsync();

            Exception exc = null;
            try
            {
                // play
                //await ctx.Message.RespondAsync($"Playing `{filename}`");
                Console.WriteLine($@"-i ""{filename}"" -ac 2 -f s16le -ar 48000 pipe:1 -loglevel quiet");
                await vnc.SendSpeakingAsync(true);
                ProcessStartInfo psi;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "ffmpeg.exe",
                        Arguments = $@"-i ""{filename}"" -ac 2 -f s16le -ar 48000 pipe:1 -loglevel quiet",
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    };
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $@"-c ""ffmpeg -i {filename} -ac 2 -f s16le -ar 48000 pipe:1 -loglevel quiet""",
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    };
                }
                else
                {
                    return;
                }
                cancelPlayingTokenSource = new CancellationTokenSource();
                Process ffmpeg = Process.Start(psi);
                Stream ffout = ffmpeg.StandardOutput.BaseStream;

                txStream = vnc.GetTransmitSink();
                txStream.VolumeModifier = VolumeLevel;

                try
                {
                    await ffout.CopyToAsync(txStream, cancellationToken: cancelPlayingTokenSource.Token);
                }
                catch (System.OperationCanceledException) { }
                try
                {
                    await txStream.FlushAsync(cancellationToken: cancelPlayingTokenSource.Token);
                }
                catch (System.OperationCanceledException) { }
                ffout.Close();
                ffmpeg.Kill();
                await vnc.WaitForPlaybackFinishAsync();
            }
            catch (Exception ex) { exc = ex; }
            finally
            {
                await vnc.SendSpeakingAsync(false);
            }

            if (exc != null)
                await ctx.RespondAsync($"An exception occured during playback: `{exc.GetType()}: {exc.Message}`");
        }
    }

    public class UngrouppedCommands : BaseCommandModule
    {
        private DateTime last_abuseascroll;
        [Command("AbuseAScroll")] // let's define this method as a command
        [Description("¦:¬)")] // this will be displayed to tell users what this command does when they invoke help
        [Aliases("aas")]
        public async Task AbuseAScroll(CommandContext ctx) // this command takes no arguments
        {
            if (ctx.Guild != null)
            {
                DateTime current_time = DateTime.UtcNow;
                const int delay = 300;
                TimeSpan time_since_last = current_time - last_abuseascroll;
                if (time_since_last.TotalSeconds >= delay)
                {
                    DiscordChannel current_voice_channel = ctx.Member.VoiceState.Channel;
                    if (current_voice_channel != null)
                    {
                        List<DiscordMember> users = current_voice_channel.Users.ToList();
                        users.RemoveAll(user => user.IsBot || user.VoiceState.IsServerDeafened || (user.VoiceState.IsSelfDeafened && user != ctx.Member));

                        if (users.Count > 0)
                        {
                            DiscordMember selected_user = users[Utilities.random.Next(users.Count)];

                            Action<MemberEditModel> action = m => { m.VoiceChannel = ctx.Guild.AfkChannel; };
                            await ctx.Member.ModifyAsync(action);
                            last_abuseascroll = current_time;

                            await ctx.TriggerTypingAsync();

                            string chosen_name;
                            if (selected_user.Nickname != null)
                                chosen_name = selected_user.Nickname;
                            else
                                chosen_name = selected_user.DisplayName;
                            var dabbing_emoji = DiscordEmoji.FromName(ctx.Client, ":dab:");
                            await ctx.RespondAsync($"{chosen_name} has been selected {dabbing_emoji}");
                        }
                        else
                        {
                            await ctx.TriggerTypingAsync();
                            var sob_emoji = DiscordEmoji.FromName(ctx.Client, ":sob:");
                            await ctx.RespondAsync($"There is no one to abuse {sob_emoji}");
                        }
                    }
                    else
                    {
                        await ctx.TriggerTypingAsync();
                        var thonking_emoji = DiscordEmoji.FromName(ctx.Client, ":thonking:");
                        await ctx.RespondAsync($"You need to be in a channel {thonking_emoji}");
                    }
                }
                else
                {
                    int total_seconds_until_next = delay - (int)time_since_last.TotalSeconds;
                    int minutes_until_next = total_seconds_until_next / 60;
                    int seconds_until_next = total_seconds_until_next % 60;

                    string time_text;
                    if (minutes_until_next > 0)
                    {
                        time_text = $"{minutes_until_next}m {seconds_until_next}s";
                    }
                    else
                    {
                        time_text = $"{seconds_until_next}s";
                    }

                    // let's trigger a typing indicator to let
                    // users know we're working
                    await ctx.TriggerTypingAsync();
                    var eyes_emoji = DiscordEmoji.FromName(ctx.Client, ":eyes:");
                    await ctx.RespondAsync($"Too soon! Try again in {time_text} {eyes_emoji}");
                }
            }
        }
    }


    [Group("RussianRoulette")]
    [Description("Russian Roulette Commands")] // this will be displayed to tell users what this command does when they invoke help
    [Aliases("Roulette")]
    public class RussianRouletteCommands : BaseCommandModule
    {
        public const int totalRounds = 6;
        public int roundCount;
        public int bulletNumber;

        public RussianRouletteCommands()
        {
            Reset();
        }

        [Command("shoot")] // let's define this method as a command
        [Description("¦:¬)")] // this will be displayed to tell users what this command does when they invoke help
        public async Task Shoot(CommandContext ctx) // this command takes no arguments
        {
            if (ctx.Guild != null)
            {
                string chosen_name;
                if (ctx.Member.Nickname != null)
                    chosen_name = ctx.Member.Nickname;
                else
                    chosen_name = ctx.Member.DisplayName;

                roundCount--;
                if (roundCount <= bulletNumber)
                {
                    Action<MemberEditModel> action = m => { m.VoiceChannel = null; };
                    await ctx.Member.ModifyAsync(action);
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"BANG! {chosen_name} got deaded!");

                    Reset();
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"Russian Roulette Reset!");
                }
                else
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"Click. {chosen_name} lives to see another day...");
                }

                if (roundCount == 1)
                {
                    Reset();
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"Only 1 round in the gun - Reset!");
                }
                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{roundCount} rounds in the gun!");
            }
        }

        [Command("status")] // let's define this method as a command
        [Description("¦:¬)")] // this will be displayed to tell users what this command does when they invoke help
        public async Task Status(CommandContext ctx) // this command takes no arguments
        {
            if (ctx.Guild != null)
            {
                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"There are {roundCount} rounds left in the gun (1 of which is a bullet)");
            }
        }

        private void Reset()
        {
            roundCount = totalRounds;
            bulletNumber = Utilities.random.Next(totalRounds);
        }
    }
}
