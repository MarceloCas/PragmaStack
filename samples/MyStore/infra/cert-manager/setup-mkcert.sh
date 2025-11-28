#!/bin/bash

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NAMESPACE="cert-manager"

echo "🔒 Setting up mkcert for local HTTPS with green padlock..."
echo ""

# Check if mkcert is installed
if ! command -v mkcert &> /dev/null; then
    echo "❌ mkcert is not installed."
    echo ""
    echo "Please install mkcert first:"
    echo ""
    echo "  Windows - Download direto (PowerShell Admin):"
    echo "    Invoke-WebRequest -Uri https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-windows-amd64.exe -OutFile mkcert.exe"
    echo "    Move-Item mkcert.exe C:\\Windows\\System32\\mkcert.exe -Force"
    echo ""
    echo "  OU Windows com winget:"
    echo "    winget install FiloSottile.mkcert"
    echo ""
    echo "  OU Windows com scoop:"
    echo "    scoop install mkcert"
    echo ""
    echo "  macOS (Homebrew):"
    echo "    brew install mkcert"
    echo ""
    echo "  Linux/WSL:"
    echo "    wget https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-linux-amd64"
    echo "    chmod +x mkcert-v1.4.4-linux-amd64"
    echo "    sudo mv mkcert-v1.4.4-linux-amd64 /usr/local/bin/mkcert"
    echo ""
    echo "After installation, run this script again."
    exit 1
fi

# Check if kubectl is available
if ! command -v kubectl &> /dev/null; then
    echo "❌ kubectl not found."
    exit 1
fi

echo "✅ mkcert found: $(mkcert -version)"
echo ""

# Step 1: Install local CA
echo "📝 Step 1/5: Installing mkcert local CA in your system..."
echo "   This adds the CA to your system trust store (browser will trust it)"
mkcert -install
echo "✅ Local CA installed"
echo ""

# Step 2: Get CA certificate files
echo "📝 Step 2/5: Locating mkcert CA files..."
CAROOT=$(mkcert -CAROOT)
echo "   CA Root directory: $CAROOT"

if [ ! -f "$CAROOT/rootCA.pem" ] || [ ! -f "$CAROOT/rootCA-key.pem" ]; then
    echo "❌ CA files not found at $CAROOT"
    exit 1
fi

echo "✅ CA files found"
echo ""

# Step 3: Create Kubernetes Secret with CA
echo "📝 Step 3/5: Creating Kubernetes Secret with mkcert CA..."

# Create namespace if it doesn't exist
kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f - > /dev/null 2>&1

# Create secret from CA files
kubectl create secret tls mkcert-ca-tls \
    --cert="$CAROOT/rootCA.pem" \
    --key="$CAROOT/rootCA-key.pem" \
    -n "$NAMESPACE" \
    --dry-run=client -o yaml | kubectl apply -f -

echo "✅ Secret 'mkcert-ca-tls' created in namespace '$NAMESPACE'"
echo ""

# Step 4: Create ClusterIssuer
echo "📝 Step 4/5: Creating ClusterIssuer 'mkcert'..."
kubectl apply -f "$SCRIPT_DIR/clusterissuer-mkcert.yaml"
echo "✅ ClusterIssuer 'mkcert' created"
echo ""

# Step 5: Wait for ClusterIssuer to be ready
echo "📝 Step 5/5: Waiting for ClusterIssuer to be ready..."
for i in {1..30}; do
    if kubectl get clusterissuer mkcert -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}' 2>/dev/null | grep -q "True"; then
        echo "✅ ClusterIssuer 'mkcert' is ready!"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "⚠️  Timeout waiting for ClusterIssuer to be ready"
        echo "   Check status with: kubectl describe clusterissuer mkcert"
        exit 1
    fi
    echo "   Waiting... ($i/30)"
    sleep 2
done
echo ""

# Summary
echo "🎉 mkcert setup completed successfully!"
echo ""
echo "📊 Summary:"
echo "   ✅ Local CA installed in your system (browsers trust it)"
echo "   ✅ CA stored as Secret 'mkcert-ca-tls' in namespace '$NAMESPACE'"
echo "   ✅ ClusterIssuer 'mkcert' is ready to issue certificates"
echo ""
echo "💡 Benefits:"
echo "   🔒 Green padlock in browser (certificates are trusted)"
echo "   ✅ Perfect for E2E tests (Playwright won't complain)"
echo "   ✅ Works offline"
echo "   ✅ Simulates production environment"
echo ""
echo "🎯 Next steps:"
echo ""
echo "1. Create a test certificate:"
echo "   kubectl apply -f example-certificate-mkcert.yaml"
echo ""
echo "2. Verify the certificate:"
echo "   kubectl get certificate -n default"
echo "   kubectl describe certificate example-mkcert-cert -n default"
echo ""
echo "3. Use in your Ingress/Gateway with annotation:"
echo "   cert-manager.io/cluster-issuer: mkcert"
echo ""
echo "4. Test in browser:"
echo "   - Linux/Mac: Add to /etc/hosts"
echo "     echo '127.0.0.1 example.mystore.local' | sudo tee -a /etc/hosts"
echo ""
echo "   - Windows (Git Bash - run as Admin):"
echo "     echo '127.0.0.1 example.mystore.local' >> /c/Windows/System32/drivers/etc/hosts"
echo ""
echo "   - Then open https://example.mystore.local in browser"
echo "   - You should see a green padlock! 🔒"
echo ""
echo "📚 View all ClusterIssuers:"
echo "   kubectl get clusterissuers"
echo ""
