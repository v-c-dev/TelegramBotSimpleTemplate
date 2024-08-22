using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class BotService : IHostedService
{
    // Private fields to store the bot client and configuration
    private readonly ITelegramBotClient _botClient;
    private readonly IConfiguration _configuration;

    // Constructor to initialize the bot client with the bot token from the configuration
    public BotService(IConfiguration configuration)
    {
        _configuration = configuration;
        _botClient = new TelegramBotClient(_configuration["TelegramBotToken"]);
    }

    // This method is called when the bot starts
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var me = await _botClient.GetMeAsync(cancellationToken); // Fetch bot details
        Console.WriteLine($"Bot started: {me.Username}"); // Print bot username

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // Receive all types of updates
        };

        // Start receiving updates (messages, commands, etc.)
        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cancellationToken
        );
    }

    // This method is called when the bot stops
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Bot stopping..."); // Print a message indicating the bot is stopping
        return Task.CompletedTask; // Return a completed task
    }

    // Handles updates received by the bot (messages, callbacks, etc.)
    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not null) // If the update is a message
        {
            await HandleMessage(botClient, update.Message, cancellationToken); // Process the message
        }
        else if (update.CallbackQuery is not null) // If the update is a callback query (from inline keyboard)
        {
            await HandleCallbackQuery(botClient, update.CallbackQuery, cancellationToken); // Process the callback query
        }
    }

    // Handles incoming messages
    private async Task HandleMessage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.Text is not { } messageText) // If the message has no text, do nothing
            return;

        var chatId = message.Chat.Id; // Get the ID of the chat where the message was sent

        Console.WriteLine($"Received a message: {messageText} in chat {chatId}"); // Print the received message and chat ID

        // Determine the command based on the first word in the message
        switch (messageText.Split(' ')[0])
        {
            case "/uid":
                await HandleUserIdCommand(botClient, message, cancellationToken); // Handle the /userid command
                break;

            case "/keyboard":
                await HandleKeyboardCommand(botClient, chatId, cancellationToken); // Handle the /keyboard command
                break;

            case "/stop":
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Farewell!", // Send a farewell message
                    cancellationToken: cancellationToken
                );
                break;

            default:
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: messageText, // Echo the received message
                    cancellationToken: cancellationToken
                );
                break;
        }
    }

    // Handles the /uid command
    // tagging a user after the command does not work
    private async Task HandleUserIdCommand(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id; // Get Chat ID

        // Is /uid command a reply to another message
        if (message.ReplyToMessage != null)
        {
            var repliedUserId = message.ReplyToMessage.From?.Id;

            if (repliedUserId != null)
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"The user ID of the person you replied to is {repliedUserId}",
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Could not retrieve the user ID.",
                    cancellationToken: cancellationToken
                );
            }
        }
        else
        {
            // If not a reply, return the ID of the sender
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Your user ID is {message.From?.Id}",
                cancellationToken: cancellationToken
            );
        }
    }

    // Handles /keyboard command, displays a numeric keyboard
    private async Task HandleKeyboardCommand(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var replyMarkup = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("1", "1"), 
                InlineKeyboardButton.WithCallbackData("2", "2"), 
                InlineKeyboardButton.WithCallbackData("3", "3") },

            new[] { InlineKeyboardButton.WithCallbackData("4", "4"), 
                InlineKeyboardButton.WithCallbackData("5", "5"), 
                InlineKeyboardButton.WithCallbackData("6", "6") },

            new[] { InlineKeyboardButton.WithCallbackData("7", "7"), 
                InlineKeyboardButton.WithCallbackData("8", "8"), 
                InlineKeyboardButton.WithCallbackData("9", "9") },

            new[] { InlineKeyboardButton.WithCallbackData("0", "0") }
        });

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Please select a number:", // Send message with inline keyboard
            replyMarkup: replyMarkup,
            cancellationToken: cancellationToken
        );
    }

    // Handles callback queries (i.e., responses from the inline keyboard)
    private async Task HandleCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message.Chat.Id; // Get the chat ID
        var data = callbackQuery.Data; // Get the callback data (number pressed)

        Console.WriteLine($"Received callback data: {data} in chat {chatId}"); // Log received data

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"You selected: {data}", // Respond with the selected number
            cancellationToken: cancellationToken
        );

        // Acknowledge the callback to remove the loading icon on the button
        await botClient.AnswerCallbackQueryAsync(
            callbackQueryId: callbackQuery.Id,
            text: $"You pressed {data}",
            cancellationToken: cancellationToken
        );
    }

    // Handles errors that occur during update handling
    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error: [{apiRequestException.ErrorCode}]\n{apiRequestException.Message}", // Handle Telegram API errors
            _ => exception.ToString() // Handle general exceptions
        };

        Console.WriteLine($"Error: {errorMessage}"); // Log the error message
        return Task.CompletedTask; // Return a completed task
    }
}