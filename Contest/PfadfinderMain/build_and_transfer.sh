#!/bin/bash
# deploy-to-pi.sh - Build and deploy PfadfinderMain to Raspberry Pi

# Colors for better output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default configuration
PI_USER="pren"
PI_DIR="/home/pren/pfadfinder"
PI_PASSWORD=""  # Will be set through user prompt

echo -e "${BLUE}===== PfadfinderMain Deployment Script =====${NC}"

# Select target Pi
echo -e "${YELLOW}Select Raspberry Pi target:${NC}"
echo "1) pren.home [default]"
echo "2) eee-05193.simple.eee.intern"
echo "3) Custom IP"
read -p "Enter your choice [1-3, default=1]: " ip_choice

# Default to option 1 if no input is provided
ip_choice=${ip_choice:-1}

case $ip_choice in
    1|"")
        PI_IP="pren.home"
        echo "Using default: pren.home"
        ;;
    2)
        PI_IP="eee-05193.simple.eee.intern"
        ;;
    3)
        read -p "Enter custom IP address: " PI_IP
        ;;
    *)
        echo -e "${RED}Invalid choice. Using default: pren.home${NC}"
        PI_IP="pren.home"
        ;;
esac

# Ask for password
read -sp "Enter Raspberry Pi password (press Enter for key-based auth): " PI_PASSWORD
echo ""

# Create SSH options
SSH_OPTS=""
SCP_OPTS=""
if [ -n "$PI_PASSWORD" ]; then
    # If using sshpass
    command -v sshpass >/dev/null 2>&1 || { 
        echo -e "${RED}Error: sshpass is required but not installed.${NC}"; 
        echo -e "${YELLOW}Install with: sudo apt install sshpass${NC}"; 
        exit 1; 
    }
    SSH_OPTS="-o StrictHostKeyChecking=no"
    SCP_OPTS="-o StrictHostKeyChecking=no"
    SSH_CMD="sshpass -p $PI_PASSWORD ssh $SSH_OPTS"
    SCP_CMD="sshpass -p $PI_PASSWORD rsync -avz --progress"
else
    # Use standard SSH with keys
    SSH_CMD="ssh -o StrictHostKeyChecking=no"
    SCP_CMD="rsync -avz --progress"
fi

echo -e "${YELLOW}===== Building and deploying to $PI_IP =====${NC}"

# Check if directories exist
if [ ! -d "PythonDetection" ]; then
  echo -e "${RED}Error: PythonDetection directory not found${NC}"
  exit 1
fi

# Create imgrec directory if it doesn't exist
mkdir -p imgrec

# Step 1: Restore packages
echo -e "${GREEN}Restoring packages...${NC}"
dotnet restore
if [ $? -ne 0 ]; then
  echo -e "${RED}Error: Failed to restore packages${NC}"
  exit 1
fi

# Step 2: Build for ARM64 Linux (Raspberry Pi 4)
echo -e "${GREEN}Building for linux-arm64...${NC}"
dotnet publish -c Release -r linux-arm64 --self-contained true
if [ $? -ne 0 ]; then
  echo -e "${RED}Error: Build failed${NC}"
  exit 1
fi

# Step 3: Create deployment directory structure
echo -e "${GREEN}Preparing deployment files...${NC}"
DEPLOY_DIR="deploy"
rm -rf $DEPLOY_DIR
mkdir -p $DEPLOY_DIR

# Copy published files
cp -r bin/Release/net8.0/linux-arm64/publish/* $DEPLOY_DIR/

# Copy Python detection files
cp -r PythonDetection $DEPLOY_DIR/

# Copy imgrec folder
mkdir -p $DEPLOY_DIR/imgrec
cp -r imgrec/* $DEPLOY_DIR/imgrec/ 2>/dev/null || :  # Ignore if empty

# Copy dependencies folder if it exists
if [ -d "dependencies" ]; then
    mkdir -p $DEPLOY_DIR/dependencies
    cp -r dependencies/* $DEPLOY_DIR/dependencies/
    echo -e "${GREEN}Copied dependencies folder${NC}"
fi

# Copy assets folder if it exists
if [ -d "assets" ]; then
    mkdir -p $DEPLOY_DIR/assets
    cp -r assets/* $DEPLOY_DIR/assets/
    echo -e "${GREEN}Copied assets folder${NC}"
else
    echo -e "${YELLOW}Warning: assets directory not found, creating empty one${NC}"
    mkdir -p $DEPLOY_DIR/assets
fi

# Copy systemd service files and setup scripts
echo -e "${GREEN}Copying systemd service files and setup scripts...${NC}"
if [ -f "pfadfinder.service" ]; then
    cp pfadfinder.service $DEPLOY_DIR/
    echo -e "${GREEN}Copied pfadfinder.service${NC}"
fi

if [ -f "pfadfinder-button-monitor.service" ]; then
    cp pfadfinder-button-monitor.service $DEPLOY_DIR/
    echo -e "${GREEN}Copied pfadfinder-button-monitor.service${NC}"
fi

if [ -f "setup_service.sh" ]; then
    cp setup_service.sh $DEPLOY_DIR/
    chmod +x $DEPLOY_DIR/setup_service.sh
    echo -e "${GREEN}Copied setup_service.sh${NC}"
fi

if [ -f "gpio_button_monitor.sh" ]; then
    cp gpio_button_monitor.sh $DEPLOY_DIR/
    chmod +x $DEPLOY_DIR/gpio_button_monitor.sh
    echo -e "${GREEN}Copied gpio_button_monitor.sh${NC}"
fi

if [ -f "X_BUTTON_RESTART_SETUP.md" ]; then
    cp X_BUTTON_RESTART_SETUP.md $DEPLOY_DIR/
    echo -e "${GREEN}Copied documentation${NC}"
fi

if [ -f "troubleshoot.sh" ]; then
    cp troubleshoot.sh $DEPLOY_DIR/
    chmod +x $DEPLOY_DIR/troubleshoot.sh
    echo -e "${GREEN}Copied troubleshoot.sh${NC}"
fi

echo -e "${GREEN}Testing SSH connection to Raspberry Pi at $PI_IP...${NC}"
if [ -n "$PI_PASSWORD" ]; then
    # Add more verbose output for debugging
    echo -e "${YELLOW}Using password authentication with sshpass${NC}"
    
    # Check if sshpass is installed
    if ! command -v sshpass &> /dev/null; then
        echo -e "${RED}Error: sshpass is not installed. Installing now...${NC}"
        sudo apt-get update && sudo apt-get install -y sshpass
        
        if [ $? -ne 0 ]; then
            echo -e "${RED}Failed to install sshpass. Try running: sudo apt-get install sshpass${NC}"
            exit 1
        fi
    fi
    
    # Use more robust options for the connection test
    sshpass -p "$PI_PASSWORD" ssh -o StrictHostKeyChecking=no -o ConnectTimeout=10 $PI_USER@$PI_IP "echo Connection successful" 2>&1
    SSH_STATUS=$?
else
    echo -e "${YELLOW}Using key-based authentication${NC}"
    ssh -o StrictHostKeyChecking=no -o ConnectTimeout=10 -q $PI_USER@$PI_IP "echo Connection successful" 2>&1
    SSH_STATUS=$?
fi

if [ $SSH_STATUS -ne 0 ]; then
    echo -e "${RED}Error: Cannot connect to Raspberry Pi at $PI_IP (Exit code: $SSH_STATUS)${NC}"
    echo "Make sure the IP/hostname is correct and SSH is enabled on the Pi."
    echo "To troubleshoot, try manually connecting: ssh $PI_USER@$PI_IP"
    exit 1
else
    echo -e "${GREEN}SSH connection successful!${NC}"
fi

# Step 4: Create directory on Pi if it doesn't exist
echo -e "${GREEN}Creating directory on Raspberry Pi...${NC}"
if [ -n "$PI_PASSWORD" ]; then
    sshpass -p "$PI_PASSWORD" ssh $SSH_OPTS $PI_USER@$PI_IP "mkdir -p $PI_DIR"
else
    ssh $SSH_OPTS $PI_USER@$PI_IP "mkdir -p $PI_DIR"
fi

# Step 5: Transfer files to Raspberry Pi
echo -e "${GREEN}Transferring files to Raspberry Pi...${NC}"
if [ -n "$PI_PASSWORD" ]; then
    sshpass -p "$PI_PASSWORD" rsync -avz --progress $SCP_OPTS $DEPLOY_DIR/ $PI_USER@$PI_IP:$PI_DIR/
else
    rsync -avz --progress $SCP_OPTS $DEPLOY_DIR/ $PI_USER@$PI_IP:$PI_DIR/
fi

if [ $? -ne 0 ]; then
  echo -e "${RED}Error: Failed to transfer files${NC}"
  exit 1
fi

# Step 6: Make the binary executable
echo -e "${GREEN}Setting executable permissions...${NC}"
if [ -n "$PI_PASSWORD" ]; then
    sshpass -p "$PI_PASSWORD" ssh $SSH_OPTS $PI_USER@$PI_IP "chmod +x $PI_DIR/PfadfinderMain"
    sshpass -p "$PI_PASSWORD" ssh $SSH_OPTS $PI_USER@$PI_IP "chmod +x $PI_DIR/setup_service.sh"
    sshpass -p "$PI_PASSWORD" ssh $SSH_OPTS $PI_USER@$PI_IP "chmod +x $PI_DIR/gpio_button_monitor.sh"
    sshpass -p "$PI_PASSWORD" ssh $SSH_OPTS $PI_USER@$PI_IP "chmod +x $PI_DIR/troubleshoot.sh"
else
    ssh $SSH_OPTS $PI_USER@$PI_IP "chmod +x $PI_DIR/PfadfinderMain"
    ssh $SSH_OPTS $PI_USER@$PI_IP "chmod +x $PI_DIR/setup_service.sh"
    ssh $SSH_OPTS $PI_USER@$PI_IP "chmod +x $PI_DIR/gpio_button_monitor.sh"
    ssh $SSH_OPTS $PI_USER@$PI_IP "chmod +x $PI_DIR/troubleshoot.sh"
fi

echo -e "${GREEN}Deployment completed successfully!${NC}"
echo -e "${YELLOW}To run the application, SSH into your Pi and run:${NC}"
echo -e "ssh $PI_USER@$PI_IP"
echo -e "cd $PI_DIR"
echo -e "./PfadfinderMain"
echo ""
echo -e "${BLUE}===== Additional Setup Options =====${NC}"
echo -e "${YELLOW}To set up systemd services for auto-start and X button restart:${NC}"
echo -e "1. SSH into your Pi: ssh $PI_USER@$PI_IP"
echo -e "2. Go to the application directory: cd $PI_DIR"
echo -e "3. Run the setup script: ./setup_service.sh"
echo -e "4. Follow the prompts to install the services"
echo ""
echo -e "${YELLOW}If you encounter issues:${NC}"
echo -e "- Run troubleshooting: ./troubleshoot.sh"
echo -e "- Test manually: ./run_manual.sh dummy"
echo -e "- Check logs: ./logs.sh"
echo ""
echo -e "${YELLOW}See X_BUTTON_RESTART_SETUP.md for detailed documentation${NC}"

# Clean up local deployment directory
echo -e "${GREEN}Cleaning up...${NC}"
rm -rf $DEPLOY_DIR

exit 0