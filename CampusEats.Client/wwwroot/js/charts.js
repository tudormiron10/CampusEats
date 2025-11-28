// ApexCharts JS Interop for Blazor

window.apexChartsInterop = {
    charts: {},

    renderBarChart: function (elementId, categories, data, title, color) {
        var element = document.querySelector("#" + elementId);
        if (!element) {
            console.warn('Chart element not found:', elementId);
            return false;
        }

        // Destroy existing chart if present
        if (this.charts[elementId]) {
            this.charts[elementId].destroy();
        }

        var options = {
            series: [{
                name: 'Orders',
                data: data
            }],
            chart: {
                type: 'bar',
                height: 350,
                toolbar: {
                    show: false
                },
                fontFamily: 'Inter, system-ui, sans-serif',
                animations: {
                    enabled: true,
                    easing: 'easeinout',
                    speed: 600
                }
            },
            plotOptions: {
                bar: {
                    borderRadius: 8,
                    columnWidth: '55%',
                }
            },
            dataLabels: {
                enabled: false
            },
            stroke: {
                width: 0
            },
            xaxis: {
                categories: categories,
                labels: {
                    style: {
                        colors: '#64748b',
                        fontSize: '12px',
                        fontWeight: 500
                    }
                },
                axisBorder: {
                    show: false
                },
                axisTicks: {
                    show: false
                }
            },
            yaxis: {
                labels: {
                    style: {
                        colors: '#64748b',
                        fontSize: '12px'
                    }
                }
            },
            fill: {
                type: 'gradient',
                gradient: {
                    shade: 'light',
                    type: 'vertical',
                    shadeIntensity: 0.25,
                    gradientToColors: [color],
                    opacityFrom: 1,
                    opacityTo: 0.85,
                }
            },
            colors: [color],
            title: {
                text: title,
                align: 'left',
                style: {
                    fontSize: '16px',
                    fontWeight: 600,
                    color: '#1e293b'
                }
            },
            tooltip: {
                theme: 'light',
                y: {
                    formatter: function (val) {
                        return val + " orders"
                    }
                }
            },
            grid: {
                borderColor: '#f1f5f9',
                strokeDashArray: 0,
                xaxis: {
                    lines: {
                        show: false
                    }
                }
            }
        };

        var chart = new ApexCharts(element, options);
        chart.render();
        this.charts[elementId] = chart;
        return true;
    },

    renderRevenueChart: function (elementId, categories, orderData, revenueData) {
        var element = document.querySelector("#" + elementId);
        if (!element) {
            console.warn('Chart element not found:', elementId);
            return false;
        }

        if (this.charts[elementId]) {
            this.charts[elementId].destroy();
        }

        var options = {
            series: [{
                name: 'Orders',
                type: 'column',
                data: orderData
            }, {
                name: 'Revenue',
                type: 'line',
                data: revenueData
            }],
            chart: {
                height: 350,
                type: 'line',
                toolbar: {
                    show: false
                },
                fontFamily: 'Inter, system-ui, sans-serif',
                animations: {
                    enabled: true,
                    easing: 'easeinout',
                    speed: 600
                },
                dropShadow: {
                    enabled: true,
                    enabledOnSeries: [1],
                    top: 3,
                    left: 0,
                    blur: 4,
                    color: '#10b981',
                    opacity: 0.2
                }
            },
            stroke: {
                width: [0, 3],
                curve: 'smooth'
            },
            plotOptions: {
                bar: {
                    borderRadius: 6,
                    columnWidth: '50%'
                }
            },
            dataLabels: {
                enabled: false
            },
            xaxis: {
                categories: categories,
                labels: {
                    style: {
                        colors: '#64748b',
                        fontSize: '11px',
                        fontWeight: 500
                    },
                    rotate: -45,
                    rotateAlways: categories.length > 12
                },
                axisBorder: {
                    show: false
                },
                axisTicks: {
                    show: false
                }
            },
            yaxis: [{
                title: {
                    text: 'Orders',
                    style: {
                        color: '#3b82f6',
                        fontSize: '12px',
                        fontWeight: 600
                    }
                },
                labels: {
                    style: {
                        colors: '#64748b',
                        fontSize: '11px'
                    },
                    formatter: function(val) {
                        return Math.round(val);
                    }
                }
            }, {
                opposite: true,
                title: {
                    text: 'Revenue',
                    style: {
                        color: '#10b981',
                        fontSize: '12px',
                        fontWeight: 600
                    }
                },
                labels: {
                    style: {
                        colors: '#64748b',
                        fontSize: '11px'
                    },
                    formatter: function (val) {
                        return '$' + val.toFixed(0)
                    }
                }
            }],
            colors: ['#3b82f6', '#10b981'],
            fill: {
                type: ['gradient', 'solid'],
                gradient: {
                    shade: 'light',
                    type: 'vertical',
                    shadeIntensity: 0.2,
                    opacityFrom: 0.9,
                    opacityTo: 0.7,
                }
            },
            tooltip: {
                shared: true,
                intersect: false,
                theme: 'light',
                style: {
                    fontSize: '12px'
                },
                y: {
                    formatter: function (val, { seriesIndex }) {
                        if (seriesIndex === 1) {
                            return '$' + val.toFixed(2)
                        }
                        return val + ' orders'
                    }
                }
            },
            grid: {
                borderColor: '#f1f5f9',
                strokeDashArray: 0,
                xaxis: {
                    lines: {
                        show: false
                    }
                },
                padding: {
                    top: 0,
                    right: 0,
                    bottom: 0,
                    left: 10
                }
            },
            legend: {
                position: 'top',
                horizontalAlign: 'right',
                fontSize: '13px',
                fontWeight: 500,
                markers: {
                    width: 10,
                    height: 10,
                    radius: 3
                },
                itemMargin: {
                    horizontal: 12
                }
            },
            markers: {
                size: [0, 4],
                strokeWidth: 0,
                hover: {
                    size: 6
                }
            }
        };

        var chart = new ApexCharts(element, options);
        chart.render();
        this.charts[elementId] = chart;
        return true;
    },

    renderDonutChart: function (elementId, labels, data, colors) {
        var element = document.querySelector("#" + elementId);
        if (!element) {
            console.warn('Chart element not found:', elementId);
            return false;
        }

        if (this.charts[elementId]) {
            this.charts[elementId].destroy();
        }

        var options = {
            series: data,
            chart: {
                type: 'donut',
                height: 200,
                fontFamily: 'Inter, system-ui, sans-serif',
                animations: {
                    enabled: true,
                    easing: 'easeinout',
                    speed: 600
                }
            },
            labels: labels,
            colors: colors || ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899', '#06b6d4'],
            plotOptions: {
                pie: {
                    donut: {
                        size: '70%',
                        labels: {
                            show: true,
                            name: {
                                show: true,
                                fontSize: '12px',
                                fontWeight: 500,
                                color: '#64748b'
                            },
                            value: {
                                show: true,
                                fontSize: '20px',
                                fontWeight: 700,
                                color: '#1e293b',
                                formatter: function (val) {
                                    return '$' + parseFloat(val).toFixed(0)
                                }
                            },
                            total: {
                                show: true,
                                label: 'Total',
                                fontSize: '12px',
                                fontWeight: 500,
                                color: '#64748b',
                                formatter: function (w) {
                                    return '$' + w.globals.seriesTotals.reduce((a, b) => a + b, 0).toFixed(0)
                                }
                            }
                        }
                    }
                }
            },
            dataLabels: {
                enabled: false
            },
            legend: {
                show: true,
                position: 'bottom',
                fontSize: '11px',
                fontWeight: 500,
                markers: {
                    width: 8,
                    height: 8,
                    radius: 2
                },
                itemMargin: {
                    horizontal: 8,
                    vertical: 4
                }
            },
            tooltip: {
                enabled: true,
                y: {
                    formatter: function (val) {
                        return '$' + val.toFixed(2)
                    }
                }
            },
            stroke: {
                width: 2,
                colors: ['#fff']
            }
        };

        var chart = new ApexCharts(element, options);
        chart.render();
        this.charts[elementId] = chart;
        return true;
    },

    renderSparkline: function (elementId, data, color) {
        var element = document.querySelector("#" + elementId);
        if (!element) {
            console.warn('Chart element not found:', elementId);
            return false;
        }

        if (this.charts[elementId]) {
            this.charts[elementId].destroy();
        }

        var options = {
            series: [{
                data: data
            }],
            chart: {
                type: 'area',
                height: 60,
                sparkline: {
                    enabled: true
                },
                animations: {
                    enabled: true,
                    easing: 'easeinout',
                    speed: 400
                }
            },
            stroke: {
                curve: 'smooth',
                width: 2
            },
            fill: {
                type: 'gradient',
                gradient: {
                    shadeIntensity: 1,
                    opacityFrom: 0.4,
                    opacityTo: 0.05,
                    stops: [0, 90, 100]
                }
            },
            colors: [color || '#3b82f6'],
            tooltip: {
                fixed: {
                    enabled: false
                },
                x: {
                    show: false
                },
                y: {
                    title: {
                        formatter: function () {
                            return '$'
                        }
                    }
                },
                marker: {
                    show: false
                }
            }
        };

        var chart = new ApexCharts(element, options);
        chart.render();
        this.charts[elementId] = chart;
        return true;
    },

    renderAreaChart: function (elementId, categories, data, title, color) {
        var element = document.querySelector("#" + elementId);
        if (!element) {
            console.warn('Chart element not found:', elementId);
            return false;
        }

        if (this.charts[elementId]) {
            this.charts[elementId].destroy();
        }

        var options = {
            series: [{
                name: title,
                data: data
            }],
            chart: {
                type: 'area',
                height: 300,
                toolbar: {
                    show: false
                },
                fontFamily: 'Inter, system-ui, sans-serif',
                animations: {
                    enabled: true,
                    easing: 'easeinout',
                    speed: 600
                },
                dropShadow: {
                    enabled: true,
                    top: 3,
                    left: 0,
                    blur: 4,
                    color: color,
                    opacity: 0.15
                }
            },
            stroke: {
                curve: 'smooth',
                width: 3
            },
            fill: {
                type: 'gradient',
                gradient: {
                    shadeIntensity: 1,
                    opacityFrom: 0.4,
                    opacityTo: 0.1,
                    stops: [0, 90, 100]
                }
            },
            colors: [color || '#3b82f6'],
            xaxis: {
                categories: categories,
                labels: {
                    style: {
                        colors: '#64748b',
                        fontSize: '11px'
                    }
                },
                axisBorder: {
                    show: false
                },
                axisTicks: {
                    show: false
                }
            },
            yaxis: {
                labels: {
                    style: {
                        colors: '#64748b',
                        fontSize: '11px'
                    },
                    formatter: function(val) {
                        return '$' + val.toFixed(0);
                    }
                }
            },
            grid: {
                borderColor: '#f1f5f9',
                strokeDashArray: 0
            },
            tooltip: {
                theme: 'light',
                y: {
                    formatter: function(val) {
                        return '$' + val.toFixed(2);
                    }
                }
            },
            markers: {
                size: 4,
                strokeWidth: 0,
                hover: {
                    size: 6
                }
            }
        };

        var chart = new ApexCharts(element, options);
        chart.render();
        this.charts[elementId] = chart;
        return true;
    },

    destroyChart: function (elementId) {
        if (this.charts[elementId]) {
            this.charts[elementId].destroy();
            delete this.charts[elementId];
        }
    },

    destroyAllCharts: function() {
        for (var id in this.charts) {
            if (this.charts.hasOwnProperty(id)) {
                this.charts[id].destroy();
                delete this.charts[id];
            }
        }
    }
};
