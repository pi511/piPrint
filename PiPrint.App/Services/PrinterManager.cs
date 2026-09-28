using System.IO;
using System.Printing;
using System.Windows.Documents;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using PiPrint.App.Models;

namespace PiPrint.App.Services;

public class PrinterInfo
{
    public string Name { get; set; } = "";
    public bool IsDefault { get; set; }
    public bool SupportsDuplex { get; set; }
    public bool IsVirtual { get; set; }

    public override string ToString() => Name;
}

public class PrinterManager
{
    public List<PrinterInfo> GetInstalledPrinters()
    {
        var printers = new List<PrinterInfo>();
        try
        {
            using var printServer = new LocalPrintServer();
            string defaultPrinterName = "";
            try
            {
                defaultPrinterName = printServer.DefaultPrintQueue?.Name ?? "";
            }
            catch
            {
            }

            var queues = printServer.GetPrintQueues(new[]
            {
                EnumeratedPrintQueueTypes.Local,
                EnumeratedPrintQueueTypes.Connections
            });

            foreach (var q in queues)
            {
                // Prevent infinite loop by not listing PiPrint or FinePrint in the target output list
                if (q.Name.Equals("PiPrint", StringComparison.OrdinalIgnoreCase) ||
                    q.Name.Equals("FinePrint Organizer", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool supportsDuplex = false;
                try
                {
                    var capabilities = q.GetPrintCapabilities();
                    supportsDuplex = capabilities?.DuplexingCapability?.Count > 1;
                }
                catch
                {
                }

                printers.Add(new PrinterInfo
                {
                    Name = q.Name,
                    IsDefault = q.Name.Equals(defaultPrinterName, StringComparison.OrdinalIgnoreCase),
                    SupportsDuplex = supportsDuplex,
                    IsVirtual = q.Name.Contains("PDF", StringComparison.OrdinalIgnoreCase) ||
                                q.Name.Contains("XPS", StringComparison.OrdinalIgnoreCase) ||
                                q.Name.Contains("OneNote", StringComparison.OrdinalIgnoreCase)
                });
            }
        }
        catch
        {
            printers.Add(new PrinterInfo { Name = "Microsoft Print to PDF", IsVirtual = true, IsDefault = true });
        }

        return printers;
    }

    public void PrintDocument(FixedDocument doc, string printerName, int copies = 1, DuplexMode duplex = DuplexMode.Simplex)
    {
        using var printServer = new LocalPrintServer();
        var queue = printServer.GetPrintQueue(printerName);

        var ticket = queue.UserPrintTicket ?? queue.DefaultPrintTicket;
        if (ticket != null)
        {
            ticket.CopyCount = copies;
            if (duplex == DuplexMode.HardwareDuplex)
            {
                ticket.Duplexing = Duplexing.TwoSidedLongEdge;
            }
            else
            {
                ticket.Duplexing = Duplexing.OneSided;
            }
        }

        var xpsWriter = PrintQueue.CreateXpsDocumentWriter(queue);
        xpsWriter.Write(doc.DocumentPaginator, ticket);
    }

    public void SaveAsXps(FixedDocument doc, string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using var xpsDoc = new XpsDocument(filePath, FileAccess.ReadWrite);
        var writer = XpsDocument.CreateXpsDocumentWriter(xpsDoc);
        writer.Write(doc.DocumentPaginator);
    }
}
