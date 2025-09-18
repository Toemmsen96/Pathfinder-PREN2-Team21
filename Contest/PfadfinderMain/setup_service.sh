#!/bin/bash

# Pfadfinder Setup Script
# This script sets up the Pfadfinder program to run as a systemd service
# and ensures proper handling of the X button restart functionality

set -e

# Configuration
SERVICE_NAME="pfadfinder"
PROGRAM_NAME="PfadfinderMain"
INSTALL_DIR="/home/pren/pfadfinder"
SERVICE_FILE="pfadfinder.service"
USER="pren"

echo "=== Pfadfinder Setup Script ==="

# Check if running as root
if [[ $EUID -eq 0 ]]; then
   echo "This script should not be run as root for safety reasons."
   echo "Please run as the 'pren' user or your regular user."
   exit 1
fi

# Check if we're in the correct directory (should contain the program files)
if [ ! -f "$PROGRAM_NAME" ]; then
    echo "Error: $PROGRAM_NAME not found in current directory."
    echo "Please run this script from the pfadfinder installation directory."
    echo "Expected location: $INSTALL_DIR"
    exit 1
fi

# Verify service file exists
if [ ! -f "$SERVICE_FILE" ]; then
    echo "Error: $SERVICE_FILE not found in current directory."
    echo "Please ensure all deployment files are present."
    exit 1
fi

echo "Found program files in current directory."
echo "Installation directory: $(pwd)"

# Check executable permissions and fix if needed
echo "Checking and fixing executable permissions..."
if [ ! -x "$PROGRAM_NAME" ]; then
    echo "Making $PROGRAM_NAME executable..."
    chmod +x "$PROGRAM_NAME"
else
    echo "$PROGRAM_NAME is already executable"
fi

# Check if it's a .NET executable and test it
echo "Testing executable..."
if file "$PROGRAM_NAME" | grep -q "ELF"; then
    echo "✓ $PROGRAM_NAME is a valid ELF executable"
else
    echo "⚠ Warning: $PROGRAM_NAME may not be a valid executable"
    echo "File type: $(file "$PROGRAM_NAME")"
fi

# Test if the program can start (quick test)
echo "Performing quick executable test..."
export DOTNET_ROOT=/home/pren/.dotnet
export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools
timeout 5s "./$PROGRAM_NAME" --help 2>/dev/null || echo "Note: Quick test failed, but this might be normal if --help isn't implemented"

# Copy service file and install it
echo "Installing systemd service..."
sudo cp "$SERVICE_FILE" "/etc/systemd/system/$SERVICE_NAME.service"

# Reload systemd
echo "Reloading systemd..."
sudo systemctl daemon-reload

# Enable the service (but don't start it yet)
echo "Enabling service..."
sudo systemctl enable "$SERVICE_NAME.service"

# Create convenience scripts
echo "Creating convenience scripts..."

# Start script
cat > "start.sh" << 'EOF'
#!/bin/bash
sudo systemctl start pfadfinder.service
echo "Pfadfinder service started"
sudo systemctl status pfadfinder.service --no-pager
EOF
chmod +x "start.sh"

# Stop script
cat > "stop.sh" << 'EOF'
#!/bin/bash
sudo systemctl stop pfadfinder.service
echo "Pfadfinder service stopped"
EOF
chmod +x "stop.sh"

# Status script
cat > "status.sh" << 'EOF'
#!/bin/bash
sudo systemctl status pfadfinder.service --no-pager
EOF
chmod +x "status.sh"

# Logs script
cat > "logs.sh" << 'EOF'
#!/bin/bash
if [ "$1" = "-f" ]; then
    sudo journalctl -u pfadfinder.service -f
else
    sudo journalctl -u pfadfinder.service --no-pager -n 50
fi
EOF
chmod +x "logs.sh"

# Manual run script (for testing)
cat > "run_manual.sh" << 'EOF'
#!/bin/bash
cd /home/pren/pfadfinder
export DOTNET_ROOT=/home/pren/.dotnet
export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools
echo "Running Pfadfinder manually (press Ctrl+C to stop)..."
./PfadfinderMain "$@"
EOF
chmod +x "run_manual.sh"

# Create udev rule for automatic start on GPIO button (optional)
echo "Creating udev rule for hardware button..."
sudo tee /etc/udev/rules.d/99-pfadfinder-button.rules > /dev/null << EOF
# This rule can be used to trigger the service start via hardware button
# Uncomment and modify according to your specific hardware setup
# ACTION=="add", SUBSYSTEM=="gpio", ATTR{direction}=="in", RUN+="/bin/systemctl start pfadfinder.service"
EOF

echo ""
echo "=== Setup Complete ==="
echo ""
echo "Installation directory: $(pwd)"
echo "Service name: $SERVICE_NAME"
echo ""
echo "Available commands:"
echo "  Start service:    ./start.sh"
echo "  Stop service:     ./stop.sh"
echo "  Check status:     ./status.sh"
echo "  View logs:        ./logs.sh [-f for follow]"
echo "  Manual run:       ./run_manual.sh [dummy] [music]"
echo "  Troubleshoot:     ./troubleshoot.sh"
echo ""
echo "The X button will now restart the program automatically."
echo "The service ensures only one instance runs at a time."
echo ""
echo "Note: The program uses PID files to prevent multiple instances."
echo "PID file location: /tmp/pfadfinder_main.pid"
echo "Restart flag file: /tmp/pfadfinder_restart.flag"
echo ""

# Ask if user wants to start the service now
read -p "Do you want to start the Pfadfinder service now? (y/N): " start_now
case $start_now in
    [Yy]* ) 
        echo "Starting Pfadfinder service..."
        
        # Additional checks before starting
        echo "Final pre-start checks..."
        ls -la "$PROGRAM_NAME"
        echo "Current user: $(whoami)"
        echo "Service user from service file: pren"
        
        sudo systemctl start $SERVICE_NAME
        sleep 3
        echo ""
        echo "Service status:"
        sudo systemctl status $SERVICE_NAME --no-pager -l
        
        # Check if service failed and provide debugging info
        if ! sudo systemctl is-active --quiet $SERVICE_NAME; then
            echo ""
            echo "⚠ Service failed to start. Debugging information:"
            echo "Recent logs:"
            sudo journalctl -u $SERVICE_NAME --no-pager -n 10
            echo ""
            echo "Troubleshooting steps:"
            echo "1. Try running manually: ./run_manual.sh dummy"
            echo "2. Check .NET installation: ls -la /home/pren/.dotnet/"
            echo "3. Check logs: ./logs.sh"
            echo "4. Check file permissions: ls -la $PROGRAM_NAME"
            echo "5. Test executable with environment: DOTNET_ROOT=/home/pren/.dotnet ./$PROGRAM_NAME dummy"
            echo "6. Run troubleshooting: ./troubleshoot.sh"
        else
            echo ""
            echo "✓ Service started successfully!"
            echo "To follow live logs: ./logs.sh -f"
        fi
        ;;
    * ) 
        echo "Service not started. Use './start.sh' to start it manually."
        echo "Or run: sudo systemctl start $SERVICE_NAME"
        echo ""
        echo "To test manually first: ./run_manual.sh dummy"
        echo "Note: Make sure .NET is installed at /home/pren/.dotnet/"
        ;;
esac
