# Wayfina Mobile App Bot

A .NET 10 service that monitors Firebase Firestore for mobile app registration requests and integrates with Zoho CRM.

## Features

- Monitors Firestore for pending requests every 5 minutes
- Creates contacts and leads in Zoho CRM
- Uploads ID photos and selfies to Zoho contacts
- Sends Telegram notifications for new registrations
- Runs as a Linux systemd service with auto-restart

## Requirements

- Linux server (Ubuntu/Debian recommended)
- .NET 10 Runtime
- 512 MB RAM (minimum)
- GitHub account (for CI/CD)
- Firebase service account key (`firebase-key.json`)

---

## Server Setup

First, SSH into your server:

```bash
ssh -i ~/AWS-Keys/LightsailDefaultKey-eu-central-1.pem ubuntu@63.181.80.37
```

### 1. Install .NET 10 Runtime

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install runtime
sudo apt-get update
sudo apt-get install -y dotnet-runtime-10.0
```

### 2. Create Application Directory

```bash
sudo mkdir -p /opt/wayfina-mobile-app-bot
sudo chown -R www-data:www-data /opt/wayfina-mobile-app-bot
```

### 3. Install Systemd Service

Copy the service file to the server and install it:

```bash
# From your local machine
scp -i ~/AWS-Keys/LightsailDefaultKey-eu-central-1.pem wayfina-mobile-app-bot.service ubuntu@63.181.80.37:/tmp/

# SSH to server
ssh -i ~/AWS-Keys/LightsailDefaultKey-eu-central-1.pem ubuntu@63.181.80.37

# Move to systemd directory
sudo cp /tmp/wayfina-mobile-app-bot.service /etc/systemd/system/
rm /tmp/wayfina-mobile-app-bot.service
sudo systemctl daemon-reload
sudo systemctl enable wayfina-mobile-app-bot
```

### 4. Configure Environment Variables

Create a `.env` file in the application directory:

```bash
sudo nano /opt/wayfina-mobile-app-bot/.env
```

Add your configuration (refer to `appsettings.json` for required values).

### 5. Add Firebase Key

Copy your Firebase service account key to the server:

```bash
scp -i ~/AWS-Keys/LightsailDefaultKey-eu-central-1.pem firebase-key.json ubuntu@63.181.80.37:/tmp/
ssh -i ~/AWS-Keys/LightsailDefaultKey-eu-central-1.pem ubuntu@63.181.80.37
sudo cp /tmp/firebase-key.json /opt/wayfina-mobile-app-bot/
sudo chown www-data:www-data /opt/wayfina-mobile-app-bot/firebase-key.json
rm /tmp/firebase-key.json
```

### 6. Manual Deploy (First Time)

```bash
# On your local machine
dotnet publish WayfinaMobileAppBot.csproj -c Release -o ./publish

# Copy to server
scp -i ~/AWS-Keys/LightsailDefaultKey-eu-central-1.pem -r ./publish/* ubuntu@63.181.80.37:/tmp/wayfina-deploy/

# SSH to server and move files
ssh -i ~/AWS-Keys/LightsailDefaultKey-eu-central-1.pem ubuntu@63.181.80.37
sudo cp -r /tmp/wayfina-deploy/* /opt/wayfina-mobile-app-bot/
sudo chown -R www-data:www-data /opt/wayfina-mobile-app-bot
rm -rf /tmp/wayfina-deploy
```

### 7. Start the Service

```bash
sudo systemctl start wayfina-mobile-app-bot
sudo systemctl status wayfina-mobile-app-bot
```

### 8. View Logs

```bash
# Real-time logs
sudo journalctl -u wayfina-mobile-app-bot -f

# Last 100 lines
sudo journalctl -u wayfina-mobile-app-bot -n 100
```

---

## GitHub Actions CI/CD Setup

Automatic deployment on every push to `main` branch.

### 1. Get Your AWS Lightsail Private Key

You already have the key at `~/AWS-Keys/LightsailDefaultKey-eu-central-1.pem`.

Get the private key content:

```bash
cat ~/AWS-Keys/LightsailDefaultKey-eu-central-1.pem
```

Copy the **entire output** including `-----BEGIN RSA PRIVATE KEY-----` and `-----END RSA PRIVATE KEY-----`.

### 2. Add GitHub Secrets

Go to your GitHub repository:

**Settings** → **Secrets and variables** → **Actions** → **New repository secret**

Add these secrets:

| Secret Name | Value |
|-------------|-------|
| `SERVER_HOST` | `63.181.80.37` |
| `SERVER_USER` | `ubuntu` |
| `SERVER_PORT` | `22` |
| `SSH_PRIVATE_KEY` | Contents of your PEM file (from step 1) |

### 3. Configure Sudo Permissions on Server

SSH into your server:

```bash
ssh -i ~/AWS-Keys/LightsailDefaultKey-eu-central-1.pem ubuntu@63.181.80.37
```

Then run:

```bash
sudo visudo
```

Add this line at the end:

```
ubuntu ALL=(ALL) NOPASSWD: /bin/systemctl stop wayfina-mobile-app-bot, /bin/systemctl start wayfina-mobile-app-bot, /bin/systemctl status wayfina-mobile-app-bot, /bin/cp, /bin/chown
```

### 4. Test Deployment

Push a commit to the `main` branch:

```bash
git add .
git commit -m "Test deployment"
git push origin main
```

Check the **Actions** tab in GitHub to see the deployment progress.

---

## Service Management

```bash
# Start service
sudo systemctl start wayfina-mobile-app-bot

# Stop service
sudo systemctl stop wayfina-mobile-app-bot

# Restart service
sudo systemctl restart wayfina-mobile-app-bot

# Check status
sudo systemctl status wayfina-mobile-app-bot

# Enable auto-start on boot
sudo systemctl enable wayfina-mobile-app-bot

# Disable auto-start
sudo systemctl disable wayfina-mobile-app-bot
```

---

## Troubleshooting

### Service won't start

Check logs for errors:

```bash
sudo journalctl -u wayfina-mobile-app-bot -n 50 --no-pager
```

### Permission denied errors

```bash
sudo chown -R www-data:www-data /opt/wayfina-mobile-app-bot
sudo chmod +x /opt/wayfina-mobile-app-bot/WayfinaMobileAppBot
```

### Firebase authentication fails

Verify the `firebase-key.json` file exists and has correct permissions:

```bash
ls -la /opt/wayfina-mobile-app-bot/firebase-key.json
```

### GitHub Actions SSH connection fails

1. Verify the server is accessible: `ssh -p PORT user@host`
2. Check if the public key is in `~/.ssh/authorized_keys`
3. Verify the private key in GitHub secrets has no extra whitespace

### Memory issues (512 MB server)

Add swap space:

```bash
sudo fallocate -l 512M /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile

# Make permanent
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

---

## Configuration Files

| File | Description |
|------|-------------|
| `appsettings.json` | Application configuration |
| `appsettings.Development.json` | Development overrides |
| `.env` | Environment variables (secrets) |
| `firebase-key.json` | Firebase service account key |
| `wayfina-mobile-app-bot.service` | Systemd service definition |
| `.github/workflows/deploy.yml` | CI/CD pipeline |

---

## License

Private - All rights reserved.
