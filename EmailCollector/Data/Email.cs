using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace EmailCollector.Models
{
    public class Email
    {
        public int ID { get; set; }
        public uint EmailUID { get; set; }

        public string FileSavePath { get; set; }

        [DisplayName("发件地址")]
        [Required]
        public string FromAddress { get; set; }

        [DisplayName("姓名")]
        [Required]
        public string SenderName { get; set; }

        [DisplayName("文件名")]
        [Required]
        public string AttachmentName { get; set; }

        [DisplayName("文件SHA1")]
        [MaxLength(20)]
        [MinLength(20)]
        [Required]
        public byte[] AttachmentSHA1 { get; set; }
    }
}
