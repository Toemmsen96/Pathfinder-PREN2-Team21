#!/bin/bash

# GPIO Button Monitor Script
# This script monitors GPIO pin for the X button and restarts the Pfadfinder program
# This is an alternative approach that runs independently of the main program

# Configuration
BUTTON_PIN=2  # GPIO pin for the X button (BCM numbering)
SERVICE_NAME="pfadfinder"
LOG_FILE="/tmp/pfadfinder_button_monitor.log"
DEBOUNCE_TIME=2  # Seconds to wait between button presses

# Logging function
log_message() {
    echo "$(date '+%Y-%m-%d %H:%M:%S') - $1" | tee -a "$LOG_FILE"
}

# Function to check if service is running
is_service_running() {
    systemctl is-active --quiet "$SERVICE_NAME"
}

# Function to restart the service
restart_service() {
    log_message "Restarting $SERVICE_NAME service..."
    systemctl stop "$SERVICE_NAME" 2>/dev/null || true
    sleep 1
    systemctl start "$SERVICE_NAME"
    if is_service_running; then
        log_message "Service restarted successfully"
    else
        log_message "ERROR: Failed to restart service"
    fi
}

# Function to setup GPIO pin
setup_gpio() {
    log_message "Setting up GPIO pin $BUTTON_PIN for button monitoring"
    
    # Export GPIO pin if not already exported
    if [ ! -d "/sys/class/gpio/gpio$BUTTON_PIN" ]; then
        echo "$BUTTON_PIN" > /sys/class/gpio/export
        sleep 0.1
    fi
    
    # Set as input with pull-up
    echo "in" > "/sys/class/gpio/gpio$BUTTON_PIN/direction"
    echo "rising" > "/sys/class/gpio/gpio$BUTTON_PIN/edge"
    
    log_message "GPIO setup complete"
}

# Function to cleanup GPIO
cleanup_gpio() {
    log_message "Cleaning up GPIO"
    if [ -d "/sys/class/gpio/gpio$BUTTON_PIN" ]; then
        echo "$BUTTON_PIN" > /sys/class/gpio/unexport 2>/dev/null || true
    fi
}

# Signal handlers
trap_exit() {
    log_message "Button monitor stopping..."
    cleanup_gpio
    exit 0
}

trap trap_exit SIGTERM SIGINT

# Main function
main() {
    log_message "Starting GPIO button monitor for pin $BUTTON_PIN"
    
    # Check if running as root (required for GPIO access)
    if [[ $EUID -ne 0 ]]; then
        echo "This script must be run as root for GPIO access"
        echo "Use: sudo $0"
        exit 1
    fi
    
    setup_gpio
    
    local last_press_time=0
    
    log_message "Monitoring button presses (Press Ctrl+C to stop)"
    
    while true; do
        # Use inotifywait to monitor GPIO value changes
        if command -v inotifywait > /dev/null; then
            inotifywait -e modify "/sys/class/gpio/gpio$BUTTON_PIN/value" > /dev/null 2>&1
        else
            # Fallback to polling if inotify-tools not available
            sleep 0.1
        fi
        
        # Read button state
        button_state=$(cat "/sys/class/gpio/gpio$BUTTON_PIN/value" 2>/dev/null || echo "0")
        current_time=$(date +%s)
        
        # Check if button was pressed (assuming active high)
        if [ "$button_state" = "1" ]; then
            # Debounce check
            if [ $((current_time - last_press_time)) -gt $DEBOUNCE_TIME ]; then
                log_message "X button pressed! Triggering service restart..."
                restart_service
                last_press_time=$current_time
            else
                log_message "Button press ignored (debounce)"
            fi
        fi
    done
}

# Run main function
main "$@"
