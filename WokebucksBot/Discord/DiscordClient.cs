using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Reflection;

namespace Swamp.WokebucksBot.Discord
{
    public class DiscordClient : IDisposable
	{
		private readonly IServiceProvider _services;
		private readonly CommandService _commands;
		private readonly DiscordSocketClient _discordSocketClient;
		private readonly ILogger<DiscordClient> _logger;
		private bool _isInitialized = false;
		private bool _disposed = false;
		
		public DiscordClient(ILogger<DiscordClient> logger, IServiceProvider serviceProvider, DiscordSocketClient socketClient, CommandService commandService)
		{
			_logger = logger;
			_services = serviceProvider;
			_commands = commandService;
			_discordSocketClient = socketClient;
		}

		public async Task InitializeAsync(string discordToken)
		{
			if(!_isInitialized)
			{
				_isInitialized = true;

				_discordSocketClient.Log += Log;
				_discordSocketClient.MessageReceived += HandleCommandAsync;

				await _discordSocketClient.LoginAsync(TokenType.Bot, discordToken);
				await _discordSocketClient.StartAsync();

				// Pass the service provider to the second parameter of
				// AddModulesAsync to inject dependencies to all modules 
				// that may require them.
				await _commands.AddModulesAsync(
					assembly: Assembly.GetEntryAssembly(),
					services: _services);
				_discordSocketClient.MessageReceived += HandleCommandAsync;
			}
		}

		public async Task HandleCommandAsync(SocketMessage socketMessage)
		{
            // Don't process the command if it was a system message
            if (socketMessage is not SocketUserMessage message)
            {
                return;
            }

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

			// Determine if the message is a command based on the prefix and make sure no bots trigger commands
			if (!(message.HasCharPrefix('$', ref argPos) ||
				message.HasMentionPrefix(_discordSocketClient.CurrentUser, ref argPos)) ||
				message.Author.IsBot)
			{
				return;
			}

			// Create a WebSocket-based command context based on the message
			var context = new SocketCommandContext(_discordSocketClient, message);

			// Pass the service provider to the ExecuteAsync method for
			// precondition checks.
			await _commands.ExecuteAsync(
				context: context,
				argPos: argPos,
				services: _services);
		}

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					_discordSocketClient?.Dispose();
				}

				_disposed = true;
			}
		}

		private Task Log(LogMessage msg)
		{
			LogLevel logLevel;
			switch (msg.Severity)
			{
				case LogSeverity.Debug:
					{
						logLevel = LogLevel.Debug;
						break;
					}
				case LogSeverity.Verbose:
					{
						logLevel = LogLevel.Trace;
						break;
					}
				case LogSeverity.Info:
					{
						logLevel = LogLevel.Information;
						break;
					}
				case LogSeverity.Warning:
					{
						logLevel = LogLevel.Warning;
						break;
					}
				case LogSeverity.Error:
					{
						logLevel = LogLevel.Error;
						break;
					}
				case LogSeverity.Critical:
					{
						logLevel = LogLevel.Critical;
						break;
					}
				default:
					{
						logLevel = LogLevel.None;
						break;
					}
			}

			_logger.Log(logLevel, msg.Exception, msg.Message);
			return Task.CompletedTask;
		}
	}
}
