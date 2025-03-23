using NetSdrApp.NetSdr;
using NetSdrApp.NetSdr.Models;

Console.WriteLine("NetSDR Client Console Application");

// Prompt the user to enter the host
Console.Write("Enter the NetSDR device host (e.g., 127.0.0.1): ");
string host = Console.ReadLine();

using var client = new NetSdrClient();

try
{
    Console.WriteLine("Connecting to NetSDR...");
    await client.ConnectAsync(host);
    Console.WriteLine("Connected successfully!");

    // Set Receiver State
    Console.WriteLine("Setting receiver state...");
    var receiverResult = await client.SetReceiverStateAsync(ReceiverState.Run, DataMode.Iq, CaptureMode.Contiguous16Bit);
    Console.WriteLine(receiverResult.IsSuccess ? "Receiver started successfully." : $"Error: {receiverResult.ErrorMessage}");

    // Set Receiver Frequency
    ulong frequencyHz;

    while (true)
    {
        Console.Write("Enter the frequency in Hz (e.g., 100000000 for 100 MHz): ");

        if (ulong.TryParse(Console.ReadLine(), out frequencyHz) && frequencyHz > 0)
        {
            break;
        }
        else
        {
            Console.WriteLine("Invalid frequency input. Please enter a valid number.");
        }
    }

    Console.WriteLine($"Setting frequency to {frequencyHz} Hz...");
    var frequencyResult = await client.SetReceiverFrequencyAsync(ChannelId.AllChannels, frequencyHz);
    Console.WriteLine(frequencyResult.IsSuccess ? "Frequency set successfully." : $"Error: {frequencyResult.ErrorMessage}");

    // Receive and Save IQ Samples
    string filePath = "iq_samples.dat";
    Console.WriteLine("Receiving IQ samples. Press Ctrl+C to stop...");

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (sender, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
        Console.WriteLine("\nCanceling IQ sample reception...");
    };

    bool result = await client.ReceiveAndSaveIQSamplesAsync(cts.Token, filePath);

    if (result)
    {
        Console.WriteLine($"IQ samples saved successfully to {filePath}.");
    }
    else
    {
        Console.WriteLine("Data wasn't received.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
finally
{
    await client.DisconnectAsync();
    Console.WriteLine("Disconnected from NetSDR.");
}