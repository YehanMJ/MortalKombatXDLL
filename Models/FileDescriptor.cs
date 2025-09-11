using System;
using System.Runtime.Serialization;

namespace MortalKombatXDLL.Models
{
    [DataContract]
    public class FileDescriptor
    {
        [DataMember] public string FileName { get; set; }
        [DataMember] public string MimeType { get; set; }
        [DataMember] public long SizeBytes { get; set; }
        [DataMember] public string Sender { get; set; }
        [DataMember] public string Room { get; set; }
        [DataMember] public FileType FileType { get; set; }
        [DataMember] public string Caption { get; set; }
        [DataMember] public DateTime UtcSent { get; set; }
    }
}