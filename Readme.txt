This is a sample Blazor Server Template that has the basic setup for Grok AI integration.

## Getting Started

Follow these steps to set up the application:

### 1. Obtain API Key
1. Visit the Grok AI website (x.ai) to obtain your API key
2. Keep your API key secure - it provides access to AI services

### 2. Configure API Key (Development)

**IMPORTANT: Never commit API keys to source control!**

For development, use User Secrets (automatically configured):

1. Right-click on the project in Visual Studio and select "Manage User Secrets"
2. Add the following JSON to the secrets file:
   ```json
   {
     "GrokApiKey": "your-api-key-here"
   }
   ```
3. Save the file - it will be stored securely outside your project directory

### 3. Production Configuration

For production deployment:
- Set the `GrokApiKey` environment variable
- Or use Azure Key Vault / other secure configuration providers
- Never store API keys in appsettings.json files

### 4. Run the Application
1. Build and run the application
2. The app will automatically use the configured API key

## Security Notes
- API keys are sensitive credentials - handle them carefully
- User Secrets are only for local development
- Production environments should use secure key management systems

Default Admin account:
Email: admin@example.com
Password: Admin123!

The ultimate goal is for the user to upload personal medical files in the system, 
have it automatically anonymize the text, convert it to a txt file, so that the data 
can be uploaded into the Grok AI.  Any medical questions will have the context of 
the data for that individual, while maintaining privacy.
