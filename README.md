# Learning Platform API

A backend API for an e-learning platform. This service manages user authentication, user course libraries, and handles the proccess of posting and enrolling in courses.

## Prerequisites
* asp.net core 10
* A running Supabase instance
* A database (postgre) : i included the script to make the database 


## Environment Variables
To run this project, you must configure the following environment variables. 
windows search -> edit the system environment variables -> advanced -> environment variables (down on the right ) -> after that choose to use the user or system one and click "new to add it".

ConnectionString ="your_database_connection_string_here"
JWT_SECRET_KEY ="your_secure_jwt_secret_here"
StupidKey ="your_supabase_service_role_key_here"
