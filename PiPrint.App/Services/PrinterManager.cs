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

        int targetCopies = Math.Max(1, copies);

        var baseTicket = queue.UserPrintTicket ?? queue.DefaultPrintTicket;
        var ticket = baseTicket != null ? baseTicket.Clone() : new PrintTicket();

        ticket.CopyCount = targetCopies;
        if (targetCopies > 1)
        {
            ticket.Collation = Collation.Collated;
        }

        if (duplex == DuplexMode.HardwareDuplex)
        {
            ticket.Duplexing = Duplexing.TwoSidedLongEdge;
        }
        else
        {
            ticket.Duplexing = Duplexing.OneSided;
        }

        PrintTicket validatedTicket = ticket;
        try
        {
            if (baseTicket != null)
            {
                var mergeResult = queue.MergeAndValidatePrintTicket(baseTicket, ticket);
                if (mergeResult.ValidatedPrintTicket != null)
                {
                    validatedTicket = mergeResult.ValidatedPrintTicket;
                    if (validatedTicket.CopyCount != targetCopies && targetCopies > 1)
                    {
                        validatedTicket.CopyCount = targetCopies;
                    }
                }
            }
        }
        catch
        {
        }

        try
        {
            doc.PrintTicket = validatedTicket;
        }
        catch
        {
        }

        bool supportsHardwareCopies = true;
        try
        {
            var caps = queue.GetPrintCapabilities(validatedTicket);
            if (caps?.MaxCopyCount == null || caps.MaxCopyCount.Value < targetCopies)
            {
                supportsHardwareCopies = false;
            }
        }
        catch
        {
            // If querying capabilities fails, attempt standard hardware copy count
        }

        if (supportsHardwareCopies)
        {
            var xpsWriter = PrintQueue.CreateXpsDocumentWriter(queue);
            xpsWriter.WritingPrintTicketRequired += (sender, args) =>
            {
                args.CurrentPrintTicket = validatedTicket;
            };
            xpsWriter.Write(doc.DocumentPaginator, validatedTicket);
        }
        else
        {
            // Fallback for software printers (e.g. PDF/XPS) or drivers with MaxCopyCount <= 1:
            // Send targetCopies individual passes
            for (int i = 0; i < targetCopies; i++)
            {
                var singlePassTicket = validatedTicket.Clone();
                singlePassTicket.CopyCount = 1;
                var xpsWriter = PrintQueue.CreateXpsDocumentWriter(queue);
                xpsWriter.WritingPrintTicketRequired += (sender, args) =>
                {
                    args.CurrentPrintTicket = singlePassTicket;
                };
                xpsWriter.Write(doc.DocumentPaginator, singlePassTicket);
            }
        }
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

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct DOCINFOW
    {
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] public string pDocName;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] public string pOutputFile;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] public string pDataType;
    }

    [System.Runtime.InteropServices.DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [System.Runtime.InteropServices.DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr hPrinter, int level, ref DOCINFOW pDocInfo);

    [System.Runtime.InteropServices.DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBuf, int cdBuf, out int pcWritten);

    public bool PrintXpsFileToPdf(string xpsFilePath, string pdfFilePath, string printerName = "Microsoft Print to PDF")
    {
        if (!File.Exists(xpsFilePath)) return false;

        if (File.Exists(pdfFilePath))
        {
            try { File.Delete(pdfFilePath); } catch { }
        }

        IntPtr hPrinter = IntPtr.Zero;
        try
        {
            if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
            {
                return false;
            }

            var di = new DOCINFOW
            {
                pDocName = Path.GetFileNameWithoutExtension(pdfFilePath),
                pOutputFile = pdfFilePath,
                pDataType = "RAW"
            };

            int docId = StartDocPrinter(hPrinter, 1, ref di);
            if (docId <= 0)
            {
                return false;
            }

            if (!StartPagePrinter(hPrinter))
            {
                EndDocPrinter(hPrinter);
                return false;
            }

            byte[] bytes = File.ReadAllBytes(xpsFilePath);
            IntPtr pUnmanagedBytes = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(bytes.Length);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, pUnmanagedBytes, bytes.Length);
                bool writeSuccess = WritePrinter(hPrinter, pUnmanagedBytes, bytes.Length, out int written);
                EndPagePrinter(hPrinter);
                EndDocPrinter(hPrinter);
                return writeSuccess && written == bytes.Length;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeCoTaskMem(pUnmanagedBytes);
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            if (hPrinter != IntPtr.Zero)
            {
                ClosePrinter(hPrinter);
            }
        }
    }
}
