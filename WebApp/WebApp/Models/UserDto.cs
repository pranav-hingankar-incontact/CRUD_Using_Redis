using Microsoft.VisualBasic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using WebApp.Models;

namespace WebApp.Models
{
    public class UserDto
    {
       

        [Required]
        public string name { get; set; } = "";

        [Required]
        public int salary { get; set; }

    }
}

//This is UserDto which do not contains the id field but I want to concanate the id also while adding to redis