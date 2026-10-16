#!/usr/bin/env bash
# Bootstraps a fresh Ubuntu LTS VPS to run the Skein server under Docker Compose.
# Run as root once:  bash bootstrap-vps.sh <deploy-user> <github-repo-url>
# Afterwards, as the deploy user:  cd ~/skein && docker compose -f deploy/docker-compose.yml up -d --build
set -euo pipefail

USER_NAME="${1:-skein}"
REPO_URL="${2:-https://github.com/arianna-arpg/skein.git}"

apt-get update
apt-get install -y ca-certificates curl gnupg ufw fail2ban unattended-upgrades git

# Docker Engine + Compose plugin from Docker's repository.
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
chmod a+r /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" > /etc/apt/sources.list.d/docker.list
apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin

# Deploy user with docker rights and key-only SSH.
if ! id "$USER_NAME" >/dev/null 2>&1; then
  adduser --disabled-password --gecos "" "$USER_NAME"
fi
usermod -aG docker "$USER_NAME"
mkdir -p "/home/$USER_NAME/.ssh"
if [ -f /root/.ssh/authorized_keys ]; then
  cp /root/.ssh/authorized_keys "/home/$USER_NAME/.ssh/authorized_keys"
fi
chown -R "$USER_NAME:$USER_NAME" "/home/$USER_NAME/.ssh"
chmod 700 "/home/$USER_NAME/.ssh"
sed -i 's/^#\?PasswordAuthentication.*/PasswordAuthentication no/' /etc/ssh/sshd_config
sed -i 's/^#\?PermitRootLogin.*/PermitRootLogin prohibit-password/' /etc/ssh/sshd_config
systemctl restart ssh || systemctl restart sshd

# Firewall: SSH plus the game port on both protocols.
ufw default deny incoming
ufw default allow outgoing
ufw allow OpenSSH
ufw allow 7777/tcp
ufw allow 7777/udp
ufw --force enable

# Automatic security updates and SSH brute-force protection.
dpkg-reconfigure -f noninteractive unattended-upgrades
systemctl enable --now fail2ban

# The repository.
sudo -u "$USER_NAME" git clone "$REPO_URL" "/home/$USER_NAME/skein" || true

echo "Done. Log in as $USER_NAME, then:"
echo "  cd ~/skein && docker compose -f deploy/docker-compose.yml up -d --build"
