// Stripe.js Integration for CampusEats
// This file handles client-side payment processing using Stripe Elements

// Use window scope to persist Stripe state across Blazor re-renders
window.stripeState = window.stripeState || {
    stripe: null,
    elements: null,
    cardElement: null
};

/**
 * Wait for Stripe to be loaded
 */
function waitForStripe() {
    return new Promise((resolve) => {
        if (typeof Stripe !== 'undefined') {
            resolve();
        } else {
            const checkStripe = setInterval(() => {
                if (typeof Stripe !== 'undefined') {
                    clearInterval(checkStripe);
                    resolve();
                }
            }, 100);
        }
    });
}

/**
 * Initialize Stripe with publishable key
 * @param {string} publishableKey - Stripe publishable key from configuration
 */
window.stripeHelper = {
    initialize: async function (publishableKey) {
        if (!publishableKey) {
            console.error('Stripe publishable key is required');
            return false;
        }
        
        // Return true if already initialized with same key
        if (window.stripeState.stripe) {
            console.log('Stripe already initialized, reusing instance');
            return true;
        }
        
        try {
            // Wait for Stripe library to be loaded
            await waitForStripe();
            
            console.log('Initializing Stripe with key:', publishableKey.substring(0, 10) + '...');
            window.stripeState.stripe = Stripe(publishableKey);
            console.log('Stripe initialized successfully');
            return true;
        } catch (error) {
            console.error('Failed to initialize Stripe:', error);
            return false;
        }
    },

    /**
     * Mount Stripe card element to a DOM element
     * @param {string} elementId - ID of the div to mount the card element
     */
    mountCardElement: function (elementId) {
        if (!window.stripeState.stripe) {
            console.error('Stripe not initialized. Call initialize() first.');
            return false;
        }

        try {
            const mountElement = document.getElementById(elementId);
            if (!mountElement) {
                console.error(`Element with ID '${elementId}' not found`);
                return false;
            }

            // If card element already exists and is mounted, don't recreate it
            if (window.stripeState.cardElement && mountElement.children.length > 0) {
                console.log('Card element already mounted, reusing instance');
                return true;
            }

            console.log('Mounting card element to:', elementId);

            // Create Elements instance if not exists
            if (!window.stripeState.elements) {
                window.stripeState.elements = window.stripeState.stripe.elements();
            }

            // Create card element with custom styling
            window.stripeState.cardElement = window.stripeState.elements.create('card', {
                style: {
                    base: {
                        fontSize: '16px',
                        color: '#002147',
                        fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
                        '::placeholder': {
                            color: '#aab7c4',
                        },
                    },
                    invalid: {
                        color: '#dc2626',
                        iconColor: '#dc2626',
                    },
                },
            });

            // Mount the card element
            window.stripeState.cardElement.mount(`#${elementId}`);

            // Listen for real-time validation errors
            window.stripeState.cardElement.on('change', function (event) {
                const displayError = document.getElementById('card-errors');
                if (displayError) {
                    if (event.error) {
                        displayError.textContent = event.error.message;
                        displayError.classList.remove('hidden');
                    } else {
                        displayError.textContent = '';
                        displayError.classList.add('hidden');
                    }
                }
            });

            console.log('Card element mounted successfully');
            return true;
        } catch (error) {
            console.error('Failed to mount card element:', error);
            return false;
        }
    },

    /**
     * Confirm card payment with Stripe
     * @param {string} clientSecret - Payment intent client secret from backend
     * @returns {Promise<object>} Payment result with success/error status
     */
    confirmCardPayment: async function (clientSecret) {
        console.log('Attempting to confirm payment...');
        console.log('Stripe state:', {
            stripe: !!window.stripeState.stripe,
            elements: !!window.stripeState.elements,
            cardElement: !!window.stripeState.cardElement
        });
        
        if (!window.stripeState.stripe || !window.stripeState.cardElement) {
            console.error('Stripe not properly initialized. stripe:', !!window.stripeState.stripe, 'cardElement:', !!window.stripeState.cardElement);
            return {
                success: false,
                error: 'Stripe not properly initialized'
            };
        }

        try {
            const { error, paymentIntent } = await window.stripeState.stripe.confirmCardPayment(clientSecret, {
                payment_method: {
                    card: window.stripeState.cardElement,
                }
            });

            if (error) {
                console.error('Payment failed:', error);
                return {
                    success: false,
                    error: error.message
                };
            }

            if (paymentIntent.status === 'succeeded') {
                console.log('Payment succeeded:', paymentIntent.id);
                return {
                    success: true,
                    paymentIntentId: paymentIntent.id
                };
            }

            return {
                success: false,
                error: 'Payment not completed'
            };
        } catch (error) {
            console.error('Unexpected error during payment confirmation:', error);
            return {
                success: false,
                error: 'An unexpected error occurred'
            };
        }
    },

    /**
     * Unmount and destroy the card element
     */
    destroy: function () {
        console.log('Destroying Stripe elements...');
        if (window.stripeState.cardElement) {
            try {
                window.stripeState.cardElement.unmount();
            } catch (e) {
                console.warn('Error unmounting card element:', e);
            }
            window.stripeState.cardElement = null;
        }
        window.stripeState.elements = null;
        // Don't destroy stripe instance to allow reuse
    }
};
