// Stripe Payment Integration for CampusEats
// This file handles Stripe Elements integration via JS Interop

let stripe = null;
let elements = null;
let paymentElement = null;

/**
 * Initialize Stripe with the publishable key
 * @param {string} publishableKey - Stripe publishable key
 */
window.initializeStripe = function (publishableKey) {
    if (!publishableKey) {
        console.error('Stripe publishable key is required');
        return false;
    }
    
    try {
        stripe = Stripe(publishableKey);
        console.log('Stripe initialized successfully');
        return true;
    } catch (error) {
        console.error('Failed to initialize Stripe:', error);
        return false;
    }
};

/**
 * Mount the Payment Element to the DOM
 * @param {string} clientSecret - The client secret from PaymentIntent
 * @param {string} elementId - The DOM element ID to mount to
 * @returns {Promise<boolean>} - True if mounted successfully
 */
window.mountPaymentElement = async function (clientSecret, elementId) {
    if (!stripe) {
        console.error('Stripe not initialized. Call initializeStripe first.');
        return false;
    }
    
    try {
        // Create elements instance with the client secret
        elements = stripe.elements({
            clientSecret: clientSecret,
            appearance: {
                theme: 'stripe',
                variables: {
                    colorPrimary: '#14213D', // oxford-blue
                    colorBackground: '#ffffff',
                    colorText: '#14213D',
                    colorDanger: '#dc2626',
                    fontFamily: 'system-ui, sans-serif',
                    borderRadius: '8px',
                    spacingUnit: '4px'
                },
                rules: {
                    '.Label': {
                        fontWeight: '600',
                        marginBottom: '8px'
                    },
                    '.Input': {
                        padding: '12px',
                        border: '2px solid #E5E1D8',
                        transition: 'border-color 0.2s ease'
                    },
                    '.Input:focus': {
                        borderColor: '#14213D'
                    }
                }
            }
        });
        
        // Create and mount the Payment Element
        paymentElement = elements.create('payment', {
            layout: 'tabs',
            wallets: {
                applePay: 'never',
                googlePay: 'never'
            }
        });
        
        const container = document.getElementById(elementId);
        if (!container) {
            console.error('Container element not found:', elementId);
            return false;
        }
        
        // Mount and wait for ready event
        return new Promise((resolve) => {
            let resolved = false;
            
            paymentElement.on('ready', () => {
                if (!resolved) {
                    resolved = true;
                    console.log('Payment Element is ready');
                    resolve(true);
                }
            });
            
            paymentElement.on('loaderror', (event) => {
                if (!resolved) {
                    resolved = true;
                    console.error('Payment Element load error:', event.error);
                    resolve(false);
                }
            });
            
            paymentElement.mount('#' + elementId);
            console.log('Payment Element mounting...');
            
            // Timeout fallback - if ready event doesn't fire in 10 seconds
            setTimeout(() => {
                if (!resolved) {
                    resolved = true;
                    console.log('Payment Element mount timeout - assuming ready');
                    resolve(true);
                }
            }, 10000);
        });
    } catch (error) {
        console.error('Failed to mount Payment Element:', error);
        return false;
    }
};

/**
 * Confirm the payment
 * @param {string} returnUrl - URL to redirect to after payment
 * @returns {Promise<object>} - Result object with success/error info
 */
window.confirmStripePayment = async function (returnUrl) {
    if (!stripe || !elements) {
        return { 
            success: false, 
            error: 'Stripe not initialized properly' 
        };
    }
    
    try {
        const { error, paymentIntent } = await stripe.confirmPayment({
            elements,
            confirmParams: {
                return_url: returnUrl
            },
            redirect: 'if_required'
        });
        
        if (error) {
            console.error('Payment failed:', error);
            return { 
                success: false, 
                error: error.message 
            };
        }
        
        if (paymentIntent) {
            console.log('Payment succeeded:', paymentIntent.status);
            return { 
                success: true, 
                status: paymentIntent.status,
                paymentIntentId: paymentIntent.id
            };
        }
        
        // If we get here, payment requires redirect (handled by Stripe)
        return { 
            success: true, 
            status: 'requires_redirect' 
        };
    } catch (error) {
        console.error('Error confirming payment:', error);
        return { 
            success: false, 
            error: error.message || 'An unexpected error occurred' 
        };
    }
};

/**
 * Cleanup Stripe elements
 */
window.destroyStripeElements = function () {
    if (paymentElement) {
        paymentElement.destroy();
        paymentElement = null;
    }
    elements = null;
    console.log('Stripe elements destroyed');
};

/**
 * Show/hide the payment element and loading indicator
 */
window.showPaymentElement = function () {
    const loader = document.getElementById('payment-loader');
    const element = document.getElementById('payment-element');
    const payButton = document.getElementById('pay-button');
    
    if (loader) loader.style.display = 'none';
    if (element) element.style.display = 'block';
    if (payButton) payButton.disabled = false;
};

