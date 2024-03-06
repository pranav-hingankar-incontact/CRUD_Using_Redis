using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Reflection;
using WebApp.Models;

namespace WebApp.Controllers
{
    [Route("api/[Controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly string connectionstring;
        private readonly IDatabase cache;

        public UserController(IConfiguration configuration)
        {
            connectionstring = configuration["ConnectionStrings:SqlServerDb"] ?? "";

            // Initialize Redis cache
            var redisConnection = ConnectionMultiplexer.Connect("localhost:6379");
            cache = redisConnection.GetDatabase();
        }

        [HttpPost]
        public IActionResult CreateUser(UserDto userDto)
        {
            try
            {
                string serializedUser = "";
                using (var connection = new SqlConnection(connectionstring))
                {
                    connection.Open();

                    string sql = "INSERT INTO users " +
                        "(name,salary) VALUES " +
                        "(@name,@salary); SELECT SCOPE_IDENTITY();";

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@name", userDto.name);
                        command.Parameters.AddWithValue("@salary", userDto.salary);

                        // Execute the command and retrieve the inserted ID
                        int insertedId = Convert.ToInt32(command.ExecuteScalar());
                        //userDto.id = insertedId;

                        // Serialize the userDto
                        serializedUser = JsonConvert.SerializeObject(userDto);

                        // Add the user data to Redis with the user ID as the key
                        string redisKey = $"User:{insertedId}";
                        cache.StringSet(redisKey, serializedUser);
                    }

                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Error", " Error");
                return BadRequest(ModelState);
            }
            return Ok();
        }
        
        [HttpGet]
        public IActionResult GetUsers()
        {

            // Data not found in cache, fetch from SQL Server
            List<User> usersFromSqlServer = new List<User>();
            try
            {
                
                    using (var connection = new SqlConnection(connectionstring))
                    {
                        connection.Open();

                        string sql = "Select * FROM users";

                        using (var command = new SqlCommand(sql, connection))
                        {
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    User user = new User
                                    {
                                        id = reader.GetInt32(0),
                                        name = reader.GetString(1),
                                        salary = reader.GetInt32(2)
                                    };

                                    usersFromSqlServer.Add(user);
                                }
                                
                            }
                        }
                        
                    }
                

            
                

                return Ok(usersFromSqlServer);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Error", " Error");
                return BadRequest(ModelState);
            }
        }

        

        [HttpGet("{id}")]
        public IActionResult GetUser(int id)
        {
            // Check if data is available in Redis cache
            string cachedData = cache.StringGet($"User:{id}");

            

            // Data not found in cache, fetch from SQL Server
            UserDto userFromSqlServer = new UserDto();
            try
            {
                string serializedUser = "";
                if (!string.IsNullOrEmpty(cachedData))
                {
                    // Data found in cache
                    Console.WriteLine($"User data for ID {id} found in Redis cache.");
                    UserDto user = JsonConvert.DeserializeObject<UserDto>(cachedData);
                    return Ok(user);
                }
                else
                {
                    using (var connection = new SqlConnection(connectionstring))
                    {
                        connection.Open();

                        string sql = "SELECT * FROM users WHERE id=@id";
                        using (var command = new SqlCommand(sql, connection))
                        {
                            command.Parameters.AddWithValue("@id", id);

                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                         
                                    userFromSqlServer.name = reader.GetString(1);
                                    userFromSqlServer.salary = reader.GetInt32(2);
                                }
                                else
                                {
                                    return BadRequest(ModelState);
                                }
                            }
                        }
                        serializedUser = JsonConvert.SerializeObject(userFromSqlServer);
                        cache.StringSet($"User:{id}", serializedUser);
                        //.. cache.CreateBatch("debezium.sink.type=redis");
                    }
                }


                return Ok(userFromSqlServer);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Error", " Error");
                return BadRequest(ModelState);
            }
        }





        [HttpPut("{id}")]
        public IActionResult UpdateUser(int id, UserDto userDto)
        {
            try
            {
                using (var connection = new SqlConnection(connectionstring))
                {
                    connection.Open();

                    string sql = "UPDATE users SET name=@name, salary=@salary WHERE id=@id";

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@name", userDto.name);
                        command.Parameters.AddWithValue("@salary", userDto.salary);
                        command.Parameters.AddWithValue("@id", id);

                        command.ExecuteNonQuery();
                    }
                }

                string cacheKey = $"User:{id}";

                if (cache.KeyExists(cacheKey))
                {
                    // Update the corresponding cache entry
                    string serializedUpdatedUserData = JsonConvert.SerializeObject(userDto);
                    cache.StringSet(cacheKey, serializedUpdatedUserData); // Update cache
                }

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Error", "Error");
                return BadRequest(ModelState);
            }

            return Ok(userDto);
        }


        [HttpDelete("{id}")]
        public IActionResult DeleteUser(int id)
        {
            try
            {
                using (var connection = new SqlConnection(connectionstring))
                {
                    connection.Open();

                    string sql = "DELETE FROM users WHERE id=@id";

                    using (var command = new SqlCommand(sql, connection))
                    {


                        command.Parameters.AddWithValue("@id", id);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Error", "Error");
                return BadRequest(ModelState);
            }

            return Ok(id);
        }
    }
}
