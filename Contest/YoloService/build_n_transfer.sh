#!/bin/bash
# build_n_transfer.sh - Build and deploy YoloService to Raspberry Pi

# Colors for better output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default configuration
PI_USER="pren"
PI_DIR="/home/pren/yoloservice"
PI_PASSWORD=""  # Will be set through user prompt

echo -e "${BLUE}===== YoloService Deployment Script =====${NC}"

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

# Check if YoloService project file exists
if [ ! -f "YoloService.csproj" ]; then
  echo -e "${RED}Error: YoloService.csproj not found. Make sure you're in the correct directory.${NC}"
  exit 1
fi

# Create models directory if it doesn't exist
mkdir -p models

# Step 1: Restore packages
echo -e "${GREEN}Restoring packages...${NC}"
dotnet restore
if [ $? -ne 0 ]; then
  echo -e "${RED}Error: Failed to restore packages${NC}"
  exit 1
fi

# Step 2: Build for ARM64 Linux (Raspberry Pi 4)
echo -e "${GREEN}Building YoloService for linux-arm64...${NC}"
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

# Copy models folder (YOLO model files)
if [ -d "models" ]; then
    mkdir -p $DEPLOY_DIR/models
    cp -r models/* $DEPLOY_DIR/models/ 2>/dev/null || :
    echo -e "${GREEN}Copied models folder${NC}"
else
    echo -e "${YELLOW}Warning: models directory not found, creating empty one${NC}"
    mkdir -p $DEPLOY_DIR/models
    echo -e "${YELLOW}Note: You'll need to upload your YOLO model files to the models directory${NC}"
fi

# Copy output folder if it exists (for detection results)
if [ -d "output" ]; then
    mkdir -p $DEPLOY_DIR/output
    cp -r output/* $DEPLOY_DIR/output/ 2>/dev/null || :
    echo -e "${GREEN}Copied output folder${NC}"
else
    mkdir -p $DEPLOY_DIR/output
    echo -e "${GREEN}Created output directory for detection results${NC}"
fi

# Copy any additional configuration files
if [ -f "appsettings.json" ]; then
    cp appsettings.json $DEPLOY_DIR/
    echo -e "${GREEN}Copied appsettings.json${NC}"
fi

if [ -f "appsettings.Production.json" ]; then
    cp appsettings.Production.json $DEPLOY_DIR/
    echo -e "${GREEN}Copied appsettings.Production.json${NC}"
fi

# Copy test images if they exist
if [ -d "test_images" ]; then
    mkdir -p $DEPLOY_DIR/test_images
    cp -r test_images/* $DEPLOY_DIR/test_images/ 2>/dev/null || :
    echo -e "${GREEN}Copied test_images folder${NC}"
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

# Step 6: Make the binary executable and Python scripts executable
echo -e "${GREEN}Setting executable permissions...${NC}"
if [ -n "$PI_PASSWORD" ]; then
    sshpass -p "$PI_PASSWORD" ssh $SSH_OPTS $PI_USER@$PI_IP "chmod +x $PI_DIR/YoloService && chmod +x $PI_DIR/*.py"
else
    ssh $SSH_OPTS $PI_USER@$PI_IP "chmod +x $PI_DIR/YoloService && chmod +x $PI_DIR/*.py"
fi

# Step 7: Setup Python virtual environment and install dependencies on Pi
echo -e "${GREEN}Setting up Python environment on Raspberry Pi...${NC}"
PYTHON_SETUP_SCRIPT="
# Create virtual environment if it doesn't exist
if [ ! -d '$PI_DIR/venv' ]; then
    echo 'Creating Python virtual environment...'
    python3 -m venv $PI_DIR/venv
fi

# Activate virtual environment and install dependencies
source $PI_DIR/venv/bin/activate

# Upgrade pip
pip install --upgrade pip

# Install required packages
echo 'Installing Python dependencies...'
pip install ultralytics onnxruntime opencv-python Pillow

echo 'Python environment setup complete!'
"

if [ -n "$PI_PASSWORD" ]; then
    sshpass -p "$PI_PASSWORD" ssh $SSH_OPTS $PI_USER@$PI_IP "$PYTHON_SETUP_SCRIPT"
else
    ssh $SSH_OPTS $PI_USER@$PI_IP "$PYTHON_SETUP_SCRIPT"
fi

echo -e "${GREEN}Deployment completed successfully!${NC}"
echo -e "${YELLOW}To run the YoloService, SSH into your Pi and run:${NC}"
echo -e "ssh $PI_USER@$PI_IP"
echo -e "cd $PI_DIR"
echo -e "./YoloService"
echo ""
echo -e "${YELLOW}Note: Make sure to:${NC}"
echo -e "1. Place your YOLO model file (e.g., pren_det_v3.onnx) in the models/ directory"
echo -e "2. Activate the Python virtual environment: source venv/bin/activate"
echo -e "3. Check that all Python dependencies are installed correctly"

# Clean up local deployment directory
echo -e "${GREEN}Cleaning up...${NC}"
rm -rf $DEPLOY_DIR

exit 0