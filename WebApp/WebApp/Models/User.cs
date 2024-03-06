using System.ComponentModel.DataAnnotations;

namespace WebApp.Models
{
    public class User
    {
       
        public int id { get; set; }

     
        public string name { get; set; } = "";

        
        public int salary { get; set; }
    }
}
