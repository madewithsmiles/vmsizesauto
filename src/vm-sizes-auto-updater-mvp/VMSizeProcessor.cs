using Grynwald.MarkdownGenerator;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;

namespace Microsoft.Azure.Compute.Supportability.Tools
{
    public class VMSizeProcessor
    {
        public static List<VMSizeDataDTO> ReadVMSizeJSON(string filePath)
        {
            List<VMSizeDataDTO> result = new List<VMSizeDataDTO>();
            try
            {
                string content = File.ReadAllText(filePath);
                JArray jArray = JArray.Parse(content);
                foreach (JToken jToken in jArray)
                {
                    if (VMSizeDataDTO.TryLoadFromJToken(jToken, out VMSizeDataDTO data))
                    {
                        result.Add(data);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Encountered exception while processing file at path: {filePath}. Exception: {ex.Message}");
            }

            return result;
        }

        public static List<VMSizeDataDTO> ProcessVMSizeDataDTOs(List<VMSizeDataDTO> inputList, bool removeConstrainedCores = true)
        {
            // Removes sizes that should not be publicly available
            // For now, remove all constrained cores
            // Sort by cpu, then memory
            List<VMSizeDataDTO> processedList = inputList
                .Where(data => data.ShouldBePubliclyVisible && (!removeConstrainedCores || !data.IsConstrainedCores))
                .OrderBy(data => data.VCPUs)
                .ThenBy(data => data.Memory)
                .ThenBy(data => data.TempStorageSSD)
                .ToList();
            return processedList;
        }
    }

    public class VMSizeDataDTO
    {
        public string Size { get; set; }
        public int VCPUs { get; set; }
        public int Memory { get; set; }
        public int TempStorageSSD { get; set; }
        public int MaxDataDisks { get; set; }
        public int IOPS { get; set; }
        public string ReadMBPS { get; set; }
        public string WriteMBPS { get; set; }
        public int MaxNICs { get; set; }
        public string ExpectedNetworkBandwidth { get; set; }
        public string __AccessLayer { get; set; }
        public string __ReleaseState { get; set; }
        public bool IsConstrainedCores { get; set; }

        // TODO: Replace ShouldBePubliclyVisible with definitive TRUE/FALSE from config. Shouldn't be decided here.
        public bool ShouldBePubliclyVisible
        {
            get { return this.__AccessLayer.Trim().ToLower() == "external" && this.__ReleaseState.Trim().ToLower() == "ga"; }
        }

        public static bool TryLoadFromJToken(JToken jtoken, out VMSizeDataDTO data)
        {
            data = new VMSizeDataDTO();
            try
            {
                // TODO: Add validation for all fields
                // Size - sometimes external name is not present..so using portal name as a fallback
                data.Size = jtoken.SelectToken("names.externalname").ToString();

                if (String.IsNullOrWhiteSpace(data.Size) && string.IsNullOrWhiteSpace(data.Size = jtoken.SelectToken("names.portalname").ToString()))
                {
                    throw new InvalidOperationException("Missing size");
                }

                // VCPUs
                data.VCPUs = int.Parse((jtoken.SelectToken("cpu.vlps") ?? throw new InvalidOperationException($"Missing cpu for {data.Size}")).ToString());

                // Memory -- NOTE that it is not yet converted to GiB
                // TODO: Investigate better way to represent transformations
                data.Memory = int.Parse((jtoken.SelectToken("memory.size.val") ?? throw new InvalidOperationException($"Missing memory for {data.Size}")).ToString());

                // TempStorageSSD
                data.TempStorageSSD = int.Parse(jtoken.SelectToken("disks.iaas_resource_disk.val").ToString());
                // MaxDataDisks
                data.MaxDataDisks = int.Parse(jtoken.SelectToken("disks.max_data_disk_count").ToString());
                // IOPS
                data.IOPS = int.Parse(jtoken.SelectToken("io_throttles.local.iops").ToString());
                // ReadMBPS // TODO: Find JSONPath query for this or calculation...
                // WriteMBPS
                data.WriteMBPS = jtoken.SelectToken("io_throttles.local.throughput.val").ToString();
                // MaxNICs
                data.MaxNICs = int.Parse(jtoken.SelectToken("network.max_nics").ToString());
                // ExpectedNetworkBandwidth
                data.ExpectedNetworkBandwidth = jtoken.SelectToken("network.throttle.val").ToString();
                // __AccessLayer
                data.__AccessLayer = jtoken.SelectToken("operations.accessibility_layer").ToString();
                // __ReleaseState
                data.__ReleaseState = jtoken.SelectToken("operations.release_state").ToString();
                // IsConstrainedCores
                data.IsConstrainedCores = bool.Parse((jtoken.SelectToken("features.constrained_cores") ?? "false").ToString());

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Encountered exception while extracting vm size data information. Exception: {ex.ToString()}");
            }
            return false;
        }

        public override string ToString()
        {
            return (new
            {
                Size = this.Size,
                VCPUs = this.VCPUs,
                Memory = this.Memory,
                TempStorageSSD = this.TempStorageSSD,
                MaxDataDisks = this.MaxDataDisks,
                IOPS = this.IOPS,
                ReadMBPS = this.ReadMBPS,
                WriteMBPS = this.WriteMBPS,
                MaxNICs = this.MaxNICs,
                ExpectedNetworkBandwidth = this.ExpectedNetworkBandwidth,
                __AccessLayer = this.__AccessLayer,
                __ReleaseState = this.__ReleaseState,
                IsConstrainedCores = this.IsConstrainedCores,
                ShouldBePubliclyVisible = this.ShouldBePubliclyVisible
            }).ToString();
        }
    }

    public interface IMappingTableMarkdownSpec
    {
        public string SpecName { get; }

        public string[] GetHeaderFields();

        public List<string> GetValues(VMSizeDataDTO data);
    }

    public static class CollectionHelpers
    {
        public static List<string> EmptyReadonlyStringList = Array.Empty<string>().ToList();
    }

    // TODO: Add additional markdown spec (for series with cached/uncached and so on..)
    public class BasicVMSizesTableMarkdownSpec : IMappingTableMarkdownSpec
    {
        public string SpecName => nameof(BasicVMSizesTableMarkdownSpec);

        public string[] GetHeaderFields()
        {
            return new string[]
            {
                "Size", "vCPU", "Memory: GiB [TODO_CONVERT]", "Temp storage (SSD) GiB",
                "Max data disks", "Max temp storage throughput: IOPS/Read MBps/Write MBps",
                "Max NICs/ Expected network bandwidth"
            };
        }

        public List<string> GetValues(VMSizeDataDTO data)
        {
            try
            {
                List<string> values = new List<string>();
                values.Add(data.Size);
                values.Add(data.VCPUs.ToString());
                values.Add(data.Memory.ToString());
                values.Add(data.TempStorageSSD.ToString());
                values.Add(data.MaxDataDisks.ToString());
                values.Add($"{data.IOPS}/TODO_FIND_MAPPING/{data.WriteMBPS}");
                values.Add($"{data.MaxNICs}/{data.ExpectedNetworkBandwidth}");
                return values;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Encountered exception while extracting values for {SpecName}. Exception: {ex}");
            }
            return CollectionHelpers.EmptyReadonlyStringList;
        }
    }

    public class VMSizesJsonMarkdownWriter
    {
        public static MdDocument GetNewIncludeDoc(List<VMSizeDataDTO> data, IMappingTableMarkdownSpec tableMarkdownSpec)
        {
            var document = new MdDocument(
                new MdParagraph(
                    new MdRawMarkdownSpan("--\n"),
                    new MdRawMarkdownSpan(" title: include file\n"),
                    new MdRawMarkdownSpan(" description: include file\n"),
                    new MdRawMarkdownSpan(" services: virtual-machines\n"),
                    new MdRawMarkdownSpan(" author: VM-SIZE-AUTOMATION\n"),
                    new MdRawMarkdownSpan(" ms.service: virtual-machines\n"),
                    new MdRawMarkdownSpan(" ms.topic: include\n"),
                    new MdRawMarkdownSpan($" ms.date: {DateTime.UtcNow:MM/dd/yyyy}\n"),
                    new MdRawMarkdownSpan(" ms.author: mimckitt;ngdiarra\n"),
                    new MdRawMarkdownSpan(" ms.custom: auto-generated include file\n"),
                    new MdRawMarkdownSpan("--\n")
                    )
                );

            var rowsValues = data.Select(singleData => tableMarkdownSpec.GetValues(singleData)).Where(singleRowAsList => singleRowAsList.Any());

            document.Root.Add(
                new MdTable(
                    headerRow: new MdTableRow(
                        cells: tableMarkdownSpec.GetHeaderFields().Select(rowElement => new MdRawMarkdownSpan(rowElement))
                    ),
                    rows: rowsValues.Select(
                        singleRow => new MdTableRow(cells: singleRow.Select(rowElement => new MdRawMarkdownSpan(rowElement)))
                        )
                    )
                );

            return document;
        }
    }
}
